import { useRef, useState } from 'react';
import { signOut, type CurrentUser, type MemberMenu } from './api';
import { portal } from './content';
import { Icon, type IconName } from './icons';

/**
 * The signed-in member shell (task 030): one menu, shown as a collapsible rail on wide screens and as a drawer on
 * phones and in the installed app. It replaces /profile as the members' way around. Only routes that still exist are
 * listed; the retired modules (games, bets, MyTuno, messages, images, labels) are deliberately absent.
 * The server decides which groups a member sees (MemberMenuAccess); every page still enforces its own rule.
 */

export type Member = Extract<CurrentUser, { authenticated: true }>;

type MenuLink = { href: string; label: string; icon: IconName; when?: keyof MemberMenu };
type MenuGroup = { key: string; label: string; links: MenuLink[] };

// Blazor pages that are still live; plain full navigations, like every link between the two front ends.
const blazor = {
  emails: '/emails',
  notifications: '/notifications',
  users: '/users',
  tracing: '/owner/tracing',
  database: '/owner/db',
} as const;

export const memberMenu: MenuGroup[] = [
  {
    key: 'member',
    label: 'Membro',
    links: [
      { href: portal.profile, label: 'Perfil', icon: 'person' },
      { href: portal.events, label: 'Atuações', icon: 'calendar' },
      { href: portal.myEnrollments, label: 'As minhas inscrições', icon: 'calendarCheck' },
      { href: portal.rehearsals, label: 'Ensaios', icon: 'clock' },
      { href: portal.music, label: 'Música', icon: 'music' },
      { href: portal.gallery, label: 'Galeria', icon: 'images' },
      { href: portal.news, label: 'Novidades', icon: 'newspaper' },
    ],
  },
  {
    key: 'tuna',
    label: 'Tuna',
    links: [
      { href: portal.members, label: 'Membros', icon: 'people' },
      { href: portal.membersHierarchy, label: 'Hierarquia', icon: 'diagram' },
      { href: portal.membersMap, label: 'Mapa de membros', icon: 'geo' },
      { href: portal.roles, label: 'Órgãos Sociais', icon: 'bank' },
      { href: portal.leaderboard, label: 'Classificação', icon: 'trophy' },
      { href: portal.hallOfFame, label: 'Hall of Fame', icon: 'star' },
      { href: portal.naipes, label: 'Naipes', icon: 'naipes' },
    ],
  },
  {
    key: 'resources',
    label: 'Recursos',
    links: [
      { href: portal.inventory, label: 'Instrumentos', icon: 'music' },
      { href: portal.shop, label: 'Loja', icon: 'bag' },
      { href: portal.documentation, label: 'Documentação', icon: 'folder', when: 'management' },
    ],
  },
  {
    key: 'management',
    label: 'Gestão',
    links: [
      { href: portal.meetings, label: 'Reuniões', icon: 'journal', when: 'management' },
      { href: portal.questions, label: 'Questões', icon: 'question', when: 'management' },
      { href: portal.logistics, label: 'Logística', icon: 'kanban', when: 'logisticsAndTreasury' },
      { href: portal.treasury, label: 'Tesouraria', icon: 'cash', when: 'logisticsAndTreasury' },
      { href: portal.requests, label: 'Pedidos', icon: 'send', when: 'admin' },
      { href: blazor.emails, label: 'Emails', icon: 'envelope', when: 'admin' },
      { href: blazor.notifications, label: 'Notificações', icon: 'bell', when: 'admin' },
    ],
  },
  {
    key: 'owner',
    label: 'Owner',
    links: [
      { href: blazor.users, label: 'Utilizadores', icon: 'personGear', when: 'owner' },
      { href: blazor.tracing, label: 'Auditoria', icon: 'shield', when: 'owner' },
      { href: blazor.database, label: 'Base de dados', icon: 'database', when: 'owner' },
    ],
  },
];

/** The groups and links this member sees; a group with nothing left is dropped. */
export function visibleMenu(menu: MemberMenu): MenuGroup[] {
  return memberMenu
    .map((g) => ({ ...g, links: g.links.filter((l) => !l.when || menu[l.when]) }))
    .filter((g) => g.links.length > 0);
}

/** The link for the current page: the longest href that is the path or a parent of it (/events/12 → Atuações). */
function currentHref(groups: MenuGroup[], path: string): string | undefined {
  return groups
    .flatMap((g) => g.links.map((l) => l.href))
    .filter((href) => path === href || path.startsWith(`${href}/`))
    .sort((a, b) => b.length - a.length)[0];
}

function MemberNav({ member, compact = false, onNavigate }: { member: Member; compact?: boolean; onNavigate?: () => void }) {
  const groups = visibleMenu(member.menu);
  const current = currentHref(groups, location.pathname.replace(/\/+$/, '') || '/');
  const [closed, setClosed] = useState<string[]>([]);

  return (
    <nav className="mnav" aria-label="Menu de membro">
      {groups.map((g) => {
        const open = compact || !closed.includes(g.key);
        const listId = `mnav-${g.key}-${compact ? 'rail' : 'list'}`;
        return (
          <section key={g.key} className="mnav__group">
            <h2 className="mnav__heading">
              <button
                type="button"
                className="mnav__toggle"
                aria-expanded={open}
                aria-controls={listId}
                disabled={compact}
                onClick={() => setClosed((c) => (c.includes(g.key) ? c.filter((k) => k !== g.key) : [...c, g.key]))}
              >
                <span className="mnav__heading-text">{g.label}</span>
                <Icon name="chevronRight" className="mnav__chevron" />
              </button>
            </h2>
            <ul id={listId} className="mnav__links" hidden={!open}>
              {g.links.map((l) => (
                <li key={l.href}>
                  <a
                    className="mnav__link"
                    href={l.href}
                    aria-current={l.href === current ? 'page' : undefined}
                    title={compact ? l.label : undefined}
                    onClick={onNavigate}
                  >
                    <Icon name={l.icon} />
                    <span className="mnav__label">{l.label}</span>
                  </a>
                </li>
              ))}
            </ul>
          </section>
        );
      })}
    </nav>
  );
}

/** Wide screens: a rail under the header, labels when expanded, icons only when collapsed. */
export function MemberRail({ member, collapsed, onToggle }: { member?: Member; collapsed: boolean; onToggle: () => void }) {
  return (
    <div className="rail">
      <button
        type="button"
        className="rail__toggle"
        aria-expanded={!collapsed}
        aria-controls="member-rail-menu"
        title={collapsed ? 'Expandir o menu' : undefined}
        onClick={onToggle}
      >
        <Icon name={collapsed ? 'chevronRight' : 'chevronLeft'} />
        <span className="rail__toggle-label">{collapsed ? 'Expandir o menu' : 'Recolher o menu'}</span>
      </button>
      <div id="member-rail-menu" className="rail__menu">
        {member ? (
          <MemberNav member={member} compact={collapsed} />
        ) : (
          <p className="rail__wait" role="status">
            <span className="sr-only">A carregar o menu…</span>
          </p>
        )}
      </div>
      {member && <SignOutButton className="rail__signout" compact={collapsed} />}
    </div>
  );
}

/** Phones and the installed app: the same menu in a drawer (native modal dialog), closed after every choice. */
export function MemberDrawer({ member }: { member?: Member }) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [open, setOpen] = useState(false);

  const show = () => {
    dialog.current?.showModal();
    setOpen(true);
  };
  const hide = () => dialog.current?.close();

  return (
    <>
      <button
        type="button"
        className="icon-btn header__menu"
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-controls="member-drawer"
        onClick={show}
      >
        <Icon name="menu" />
        <span className="sr-only">Abrir o menu de membro</span>
      </button>
      <dialog
        id="member-drawer"
        ref={dialog}
        className="drawer"
        aria-label="Menu de membro"
        onClose={() => setOpen(false)}
        onClick={(e) => {
          if (e.target === e.currentTarget) hide(); // a tap on the backdrop
        }}
      >
        <div className="drawer__panel">
          <div className="drawer__top">
            {member ? <MemberIdentity member={member} onNavigate={hide} /> : <span className="drawer__spacer" />}
            <button type="button" className="icon-btn" onClick={hide}>
              <Icon name="close" />
              <span className="sr-only">Fechar o menu</span>
            </button>
          </div>
          <div className="drawer__menu">
            {member ? (
              <MemberNav member={member} onNavigate={hide} />
            ) : (
              <p className="note" role="status">
                A carregar o menu…
              </p>
            )}
          </div>
          {member && <SignOutButton className="drawer__signout" />}
        </div>
      </dialog>
    </>
  );
}

const DEFAULT_AVATAR = '/images/default-avatar.webp';

/** Who is signed in: avatar, name and the first category, linking to the account page. */
export function MemberIdentity({ member, onNavigate }: { member?: Member; onNavigate?: () => void }) {
  if (!member) {
    return (
      <span className="idchip idchip--pending" role="status">
        <span className="idchip__avatar skeleton" aria-hidden="true" />
        <span className="sr-only">A verificar a sessão…</span>
      </span>
    );
  }

  return (
    <a className="idchip" href={portal.profile} onClick={onNavigate}>
      <img
        className="idchip__avatar"
        src={member.avatarUrl}
        alt=""
        width="36"
        height="36"
        onError={(e) => {
          if (!e.currentTarget.src.endsWith(DEFAULT_AVATAR)) e.currentTarget.src = DEFAULT_AVATAR;
        }}
      />
      <span className="idchip__text">
        <span className="idchip__name">{member.displayName}</span>
        {member.categories[0] && <span className="idchip__rank">{member.categories[0]}</span>}
      </span>
      <span className="sr-only"> (o meu perfil)</span>
    </a>
  );
}

/** POST /auth/logout with its antiforgery token; the server redirects to /. */
export function SignOutButton({ className = '', compact = false }: { className?: string; compact?: boolean }) {
  const [state, setState] = useState<'idle' | 'busy' | 'failed'>('idle');

  return (
    <div className={`signout ${className}`}>
      <button
        type="button"
        className="mnav__link signout__btn"
        disabled={state === 'busy'}
        title={compact ? 'Terminar sessão' : undefined}
        onClick={async () => {
          setState('busy');
          if (!(await signOut())) setState('failed');
        }}
      >
        <Icon name="logout" />
        <span className="mnav__label">Terminar sessão</span>
      </button>
      {state === 'failed' && (
        <p className="signout__error" role="alert">
          Não foi possível terminar a sessão. Tente de novo.
        </p>
      )}
    </div>
  );
}
