import { useEffect, useRef, useState } from 'react';
import { useCurrentUser } from '../components/AppShared';
import { loginToProfile, portal } from '../data/content';
import { Icon, type IconName } from '../components/icons';
import { MemberBadge } from '../components/MemberBadge';
import { memberAreaApi, type MyProfile } from '../data/memberAreaApi';
import { SignOutButton } from '../components/MemberShell';
import { Notice, RetryNotice } from '../components/Notice';
import { InstrumentsSection, SubscriptionSection } from '../components/ProfileInstruments';
import { PasskeySection, SecuritySection } from '../components/ProfileSecurity';
import { PersonalSection } from '../components/ProfilePersonal';
import { ProfilePhoto } from '../components/ProfilePhoto';
import { PushSection } from '../components/ProfilePush';
import { TunaSection } from '../components/ProfileTuna';
import { Loading } from '../components/ui/Loading';
import { PageHead } from '../components/ui/PageHead';

/**
 * /profile - the members-only corner of a public portal. RTUB has no public accounts: the tuna creates its members'
 * logins, so a visitor is told that first and keeps the public portal one tap away. Signing in is the React /login.
 *
 * Signed in, it is the member's own profile: who they are, their rank, and
 * the sections they edit - the header (photo included), Pessoal, Informação da Tuna, Instrumentos, Notificações por
 * email, Notificações push and Segurança. The server decides
 * every rule (MyProfileService); a disabled field here is only a convenience. The members' menu is the member shell.
 */
export default function Profile() {
  const { user, failed, retry } = useCurrentUser();
  const signedIn = user?.authenticated === true;

  useEffect(() => {
    document.title = signedIn ? 'O meu perfil · RTUB' : 'Área de membros · RTUB';
  }, [signedIn]);

  return (
    <section className="page wrap profile-page" aria-labelledby="profile-title">
      <PageHead
        plain
        eyebrow={user ? 'Área de membros' : 'RTUB'}
        title={!user ? 'Área de membros' : signedIn ? 'O meu perfil' : 'Área reservada a membros'}
        titleId="profile-title"
      />

      {failed ? (
        <RetryNotice onRetry={retry}>Não conseguimos confirmar a sessão agora.</RetryNotice>
      ) : !user ? (
        <SessionSkeleton />
      ) : user.authenticated ? (
        <MyProfilePage />
      ) : (
        <div className="members">
          <div className="members__main">
            <SignedOut />
          </div>
          <PublicShortcuts />
        </div>
      )}
    </section>
  );
}

/** Same footprint as the account card, so nothing jumps when the session arrives. */
function SessionSkeleton() {
  return (
    <div className="account__card account__card--skeleton" role="status">
      <span className="skeleton skeleton--avatar" aria-hidden="true" />
      <span className="skeleton__lines" aria-hidden="true">
        <span className="skeleton skeleton--line" />
        <span className="skeleton skeleton--line skeleton--short" />
      </span>
      <span className="sr-only">A verificar a sessão…</span>
    </div>
  );
}

// ---------- signed in: the profile editor ----------

function MyProfilePage() {
  const [profile, setProfile] = useState<MyProfile | 'failed'>();
  const security = useRef<HTMLElement>(null);

  useEffect(() => {
    memberAreaApi.profile().then((o) => setProfile(o.kind === 'ok' ? o.data : 'failed'));
  }, []);

  if (profile === undefined) return <Loading label="A carregar o perfil…" />;
  if (profile === 'failed') {
    return (
      <Notice>Não foi possível carregar o perfil. Tenta outra vez daqui a pouco.</Notice>
    );
  }

  const m = profile.member;
  const goToSecurity = () => {
    security.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    security.current?.querySelector('input')?.focus({ preventScroll: true });
  };

  return (
    <div className="profile">
      {profile.requirePasswordChange && (
        <div className="notice notice--warning profile__warning" role="alert">
          <p>
            <strong>Palavra-passe temporária.</strong> Escolhe já uma palavra-passe tua.
          </p>
          <button type="button" className="btn btn--primary btn--sm" onClick={goToSecurity}>
            <Icon name="lock" />
            Mudar a palavra-passe
          </button>
        </div>
      )}

      <div className="profile__top">
        <div className="account__card profile__identity">
          <ProfilePhoto profile={profile} onChange={(avatarUrl) => setProfile({ ...profile, member: { ...m, avatarUrl } })} />
          <div className="account__who">
            <p className="account__name">{m.displayName}</p>
            {m.fullName && <p className="account__full">{m.fullName}</p>}
            {m.badges.length + m.positions.length > 0 ? (
              <span className="member-badges">
                {m.badges.map((b) => (
                  <MemberBadge key={b.kind} kind={b.kind}>{b.label}</MemberBadge>
                ))}
                {m.positions.map((p) => (
                  <MemberBadge key={p} kind="position">{p}</MemberBadge>
                ))}
              </span>
            ) : (
              <p className="note">Ainda sem categoria atribuída.</p>
            )}
          </div>
        </div>
        <RankCard profile={profile} />
      </div>

      <div className="account__actions profile__actions">
        <SignOutButton className="profile__signout" buttonClassName="btn btn--ghost btn--sm" />
      </div>

      <PersonalSection profile={profile} onSaved={setProfile} />
      <TunaSection profile={profile} onSaved={setProfile} />
      <InstrumentsSection profile={profile} onChange={(instruments) => setProfile({ ...profile, instruments })} />
      <SubscriptionSection profile={profile} onChange={(subscribed) => setProfile({ ...profile, subscribed })} />
      <PushSection />
      <PasskeySection />
      <SecuritySection ref={security} onChanged={() => setProfile({ ...profile, requirePasswordChange: false })} />
    </div>
  );
}

/** "Nível de alcoolismo": the leaderboard's level, XP and the way to the next level. */
function RankCard({ profile }: { profile: MyProfile }) {
  const r = profile.rank;
  return (
    <div className="profile-rank" aria-label="Nível">
      <p className="profile-rank__level">
        <span>Nível</span>
        <strong>{r.level}</strong>
      </p>
      <div className="profile-rank__body">
        <p className="profile-rank__name">{r.name}</p>
        <p className="note">{r.xp} XP no total</p>
        {r.maxLevel ? (
          <p className="profile-rank__next">Chegaste ao nível máximo.</p>
        ) : (
          <>
            <progress className="profile-rank__bar" max={100} value={r.percent} aria-label="Progresso para o próximo nível" />
            <p className="note">
              {r.xpInLevel} / {r.xpForLevel} XP · faltam {r.xpToNext} XP para o nível {r.level + 1}
              {r.nextName && ` (${r.nextName})`}
            </p>
          </>
        )}
        <a className="profile-rank__link" href={portal.leaderboard}>
          Ver a classificação
        </a>
      </div>
    </div>
  );
}

function SignedOut() {
  return (
    <div className="account">
      <div className="account__card account__card--info">
        <Icon name="lock" className="account__lock" />
        <div className="account__who">
          <p>
            Esta área serve apenas os membros da RTUB. O acesso é criado pela própria tuna; não há registo público.
          </p>
          <p className="account__muted">Se és membro, entra para consultar ensaios, atuações e o resto da área de membros.</p>
        </div>
      </div>
      <div className="account__actions">
        <a className="btn btn--ghost" href={loginToProfile}>
          <Icon name="login" />
          Entrar como membro
        </a>
      </div>
    </div>
  );
}

const shortcuts: { href: string; icon: IconName; label: string }[] = [
  { href: portal.request, icon: 'send', label: 'Pedir uma atuação' },
  { href: '/#events', icon: 'calendar', label: 'Próximas atuações' },
  { href: portal.music, icon: 'music', label: 'Discografia' },
  { href: portal.roles, icon: 'bank', label: 'Órgãos Sociais' },
  { href: '/#gallery', icon: 'images', label: 'Galeria' },
];

/** The public portal, always within reach: the main paths for everyone who is not a member. */
function PublicShortcuts() {
  return (
    <nav className="shortcuts" aria-labelledby="shortcuts-title">
      <h2 id="shortcuts-title" className="request__title">
        No portal
      </h2>
      <ul>
        {shortcuts.map((s, i) => (
          <li key={s.href}>
            <a className={i === 0 ? 'shortcut shortcut--primary' : 'shortcut'} href={s.href}>
              <Icon name={s.icon} />
              {s.label}
              <Icon name="arrow" className="shortcut__go" />
            </a>
          </li>
        ))}
      </ul>
    </nav>
  );
}
