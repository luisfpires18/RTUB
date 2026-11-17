// Helpers shared by the event discussion pieces: limits, time label, text with mentions, author line.

import type { KeyboardEvent, ReactNode } from 'react';
import type { EventAuthor, EventPost, Outcome } from '../data/eventsApi';
import { MemberBadge, badgeKind } from './MemberBadge';
import { outcomeText } from '../data/api';

export const BODY_MAX = 5000;
export const COMMENT_MAX = 2000;

export const problem = (o: Outcome<unknown>) =>
  outcomeText(o, 'guardar', { forbidden: 'Não tens permissão para isto.', notfound: 'Isto já não existe. Recarrega a página.' });

/** "agora", "há 5 min", "há 3 h", "ontem, 21:10", then "12 de outubro, 21:10" (and the year when it is not this one). */
export function when(iso: string, now = new Date()) {
  const d = new Date(iso);
  const minutes = Math.round((now.getTime() - d.getTime()) / 60000);
  const time = d.toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' });
  if (minutes < 1) return 'agora';
  if (minutes < 60) return `há ${minutes} min`;
  const yesterday = new Date(now);
  yesterday.setDate(now.getDate() - 1);
  if (d.toDateString() === now.toDateString()) return `há ${Math.round(minutes / 60)} h`;
  if (d.toDateString() === yesterday.toDateString()) return `ontem, ${time}`;
  const day = d.toLocaleDateString('pt-PT', { day: 'numeric', month: 'long', ...(d.getFullYear() !== now.getFullYear() ? { year: 'numeric' } : {}) });
  return `${day}, ${time}`;
}

/** @mentions stand out, as on the old page; the text itself is never HTML. */
export function Text({ children }: { children: string }) {
  return (
    <p className="talk__text">
      {children.split(/(@[\p{L}\p{N}_]+)/u).map((part, i) =>
        i % 2 === 1 ? (
          <span key={i} className="talk__mention">
            {part}
          </span>
        ) : (
          part
        ),
      )}
    </p>
  );
}

/** A title taken from the start of the text (when none was given) is not repeated above it. */
export const showsTitle = (p: EventPost) => !p.body.replace(/\s+/g, ' ').trim().startsWith(p.title.replace(/…$/, ''));

export function Who({ author, at, edited, children }: { author: EventAuthor; at: string; edited: boolean; children?: ReactNode }) {
  return (
    <header className="talk__who">
      <img className="talk__avatar" src={author.avatarUrl} alt="" loading="lazy" width="44" height="44" />
      <div className="talk__meta">
        <p className="talk__name" title={author.fullName ?? undefined}>
          {author.name}
          {author.badge && <MemberBadge kind={badgeKind(author.badge)}>{author.badge}</MemberBadge>}
        </p>
        <p className="talk__time">
          <time dateTime={at} title={new Date(at).toLocaleString('pt-PT')}>
            {when(at)}
          </time>
          {edited && ' · editado'}
        </p>
      </div>
      {children}
    </header>
  );
}

/** Submit on Ctrl/⌘+Enter, as chat boxes do; Enter alone is a new line. */
export const submitOnCtrlEnter = (e: KeyboardEvent<HTMLTextAreaElement>) => {
  if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
    e.preventDefault();
    e.currentTarget.form?.requestSubmit();
  }
};
