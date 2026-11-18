// A member's round photo; falls back to the default picture when missing or broken.
import { DEFAULT_AVATAR } from '../../data/membersApi';

/** `className`: "who__avatar" (member lists, chats) or "mtg-face" (meetings). */
export function Avatar({ src, size = 32, className }: { src: string | null; size?: number; className: string }) {
  return (
    <img
      className={className}
      src={src || DEFAULT_AVATAR}
      alt=""
      width={size}
      height={size}
      loading="lazy"
      onError={(e) => {
        if (!e.currentTarget.src.endsWith(DEFAULT_AVATAR)) e.currentTarget.src = DEFAULT_AVATAR;
      }}
    />
  );
}
