// A member's round photo; falls back to the default picture when missing or broken.
import { DEFAULT_AVATAR } from '../../data/membersApi';

/**
 * `className`: "who__avatar" (member lists, chats), "mtg-face" (meetings), or none (a plain 28px picker photo).
 * `eager`: no lazy loading (the signed-in chip, the profile photo). `alt`: a real description (default: decorative).
 * `noFallback`: a broken picture stays broken instead of swapping to the default.
 */
export function Avatar({
  src,
  size = 32,
  className,
  eager,
  alt = '',
  noFallback,
}: {
  src: string | null;
  size?: number;
  className?: string;
  eager?: boolean;
  alt?: string;
  noFallback?: boolean;
}) {
  return (
    <img
      className={className}
      src={src || DEFAULT_AVATAR}
      alt={alt}
      width={size}
      height={size}
      loading={eager ? undefined : 'lazy'}
      onError={
        noFallback
          ? undefined
          : (e) => {
              if (!e.currentTarget.src.endsWith(DEFAULT_AVATAR)) e.currentTarget.src = DEFAULT_AVATAR;
            }
      }
    />
  );
}
