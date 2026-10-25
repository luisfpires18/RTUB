import { useRef, useState } from 'react';
import { signOut, type CurrentUser, type MemberMenu } from './api';
import { portal } from './content';
import { Icon, type IconName } from './icons';
import { MemberBadge, badgeKind } from './MemberBadge';
import { unreadLabel, useUnread } from './unread';

/**
 * The signed-in member shell (task 030, reshaped in 044): the header carries [inbox] [profile] [menu] on the right, and
 * the one member menu is a drawer from the right on every screen size (no permanent sidebar). Only routes that still
 * exist are listed; the retired modules (games, bets, MyTuno, images, labels) are deliberately absent. Perfil and
 * Mensagens are not in the menu: the profile chip and the inbox button are their direct entries.
 * The server decides which groups a member sees (MemberMenuAccess); every page still enforces its own rule.
 */

export type Member = Extract<CurrentUser, { authenticated: true }>;

type MenuLink = { href: string; label: string; icon: IconName; when?: keyof MemberMenu };
type MenuGroup = { key: string; label: string; links: MenuLink[] };

export const memberMenu: MenuGroup[] = [
  {
    key: 'member',
    label: 'Membro',
    links: [
      { href: portal.events, label: 'Atuações', icon: 'calendar' },
      { href: portal.rehearsals, label: 'Ensaios', icon: 'clock' },
      { href: portal.music, label: 'Música', icon: 'music' },
      { href: portal.gallery, label: 'Galeria', icon: 'images' },
      { href: portal.news, label: 'Novidades', icon: 'newspaper' },
      { href: portal.shop, label: 'Loja', icon: 'bag' },
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
      { href: portal.inventory, label: 'Instrumentos', icon: 'music' },
    ],
  },
  {
    key: 'management',
    label: 'Gestão',
    links: [
      { href: portal.meetings, label: 'Reuniões', icon: 'journal', when: 'management' },
      { href: portal.documentation, label: 'Documentação', icon: 'folder', when: 'management' },
      { href: portal.questions, label: 'Questões', icon: 'question', when: 'management' },
      { href: portal.logistics, label: 'Logística', icon: 'kanban', when: 'logisticsAndTreasury' },
      { href: portal.treasury, label: 'Tesouraria', icon: 'cash', when: 'logisticsAndTreasury' },
      { href: portal.requests, label: 'Pedidos', icon: 'send', when: 'admin' },
    ],
  },
  {
    key: 'owner',
    label: 'Owner',
    links: [
      { href: portal.users, label: 'Utilizadores', icon: 'personGear', when: 'owner' },
      { href: portal.ownerTracing, label: 'Auditoria', icon: 'shield', when: 'owner' },
      { href: portal.ownerDb, label: 'Base de dados', icon: 'database', when: 'owner' },
      { href: portal.emails, label: 'Emails', icon: 'envelope', when: 'owner' },
      { href: portal.notifications, label: 'Notificações', icon: 'bell', when: 'owner' },
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

function MemberNav({ member, onNavigate }: { member: Member; onNavigate?: () => void }) {
  const groups = visibleMenu(member.menu);
  const current = currentHref(groups, location.pathname.replace(/\/+$/, '') || '/');
  const [closed, setClosed] = useState<string[]>([]);

  return (
    <nav className="mnav" aria-label="Menu de membro">
      {groups.map((g) => {
        const open = !closed.includes(g.key);
        const listId = `mnav-${g.key}`;
        return (
          <section key={g.key} className="mnav__group">
            <h2 className="mnav__heading">
              <button
                type="button"
                className="mnav__toggle"
                aria-expanded={open}
                aria-controls={listId}
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

/** The member menu: a drawer from the right (native modal dialog: Esc, focus containment, inert page), closed after every choice. */
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
        className="icon-btn"
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
            <span className="drawer__title">Menu</span>
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

/** Header inbox: straight to the messages page, with the unread message count (see unread.ts) as a badge. */
export function MemberInbox() {
  const unread = useUnread();
  const label = unreadLabel(unread);
  return (
    <a className="icon-btn inbox" href={portal.messages} aria-current={location.pathname === portal.messages ? 'page' : undefined}>
      <Icon name="inbox" />
      {label && (
        <span className="inbox__badge" aria-hidden="true">
          {label}
        </span>
      )}
      <span className="sr-only">{unread > 0 ? `Mensagens, ${unread} por ler` : 'Mensagens'}</span>
    </a>
  );
}

const DEFAULT_AVATAR = '/images/default-avatar.webp';

/** Header category: the member's first category as the shared badge, its own control between the inbox and the profile. */
export function MemberCategory({ member }: { member: Member }) {
  const label = member.categories[0];
  return label ? (
    <span className="header__category">
      <MemberBadge kind={badgeKind(label)}>{label}</MemberBadge>
    </span>
  ) : null;
}

/** Who is signed in: avatar and name. One click goes straight to /profile. */
export function MemberIdentity({ member }: { member?: Member }) {
  if (!member) {
    return (
      <span className="idchip idchip--pending" role="status">
        <span className="idchip__avatar skeleton" aria-hidden="true" />
        <span className="sr-only">A verificar a sessão…</span>
      </span>
    );
  }

  return (
    <a className="idchip" href={portal.profile}>
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
      </span>
      <span className="sr-only"> (o meu perfil)</span>
    </a>
  );
}

/** POST /auth/logout with its antiforgery token; the server redirects to /. */
export function SignOutButton({ className = '', buttonClassName = 'mnav__link signout__btn' }: { className?: string; buttonClassName?: string }) {
  const [state, setState] = useState<'idle' | 'busy' | 'failed'>('idle');

  return (
    <div className={`signout ${className}`}>
      <button
        type="button"
        className={buttonClassName}
        disabled={state === 'busy'}
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
