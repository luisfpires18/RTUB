import * as L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { useEffect, useRef, useState } from 'react';
import { Loading } from './App';
import { portal } from './content';
import { Icon } from './icons';
import { MemberFace } from './MemberDialogs';
import { memberAreaApi, type MapCity, type MemberMap, type Person } from './memberAreaApi';
import { DEFAULT_AVATAR } from './membersApi';
import { loginTo } from './musicApi';

// Leaflet ships in this page's own chunk (vendor-leaflet), so no other page loads it.
const PORTUGAL: L.LatLngTuple = [39.5, -8.0];
const POPUP_LIMIT = 10;
const TILES = 'https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png';
const ATTRIBUTION =
  '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> &copy; <a href="https://carto.com/attributions">CARTO</a>';

/**
 * /members/map (task 032; was the Blazor /member/map). Members by the city on their profile, one marker per city that
 * the geocoding cache already knows; the others wait for the background worker and are listed below. City-level
 * only: nobody's address or own position is ever on this page. Signed-in members only.
 */
export default function MembersMap() {
  const [map, setMap] = useState<MemberMap | 'signin' | null>();
  const [focus, setFocus] = useState<MapCity>();

  useEffect(() => {
    document.title = 'Mapa de membros · RTUB';
    memberAreaApi.map().then((o) => setMap(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : null));
  }, []);

  return (
    <section className="page wrap events-page map-page" aria-labelledby="map-title">
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Área de membros</p>
          <h1 id="map-title" className="page__title">
            Mapa de membros
          </h1>
          <p className="page__lead">De onde vêm os membros da RTUB, cidade a cidade.</p>
        </div>
        <div className="events-page__actions">
          <a className="btn btn--ghost btn--sm" href={portal.members}>
            <Icon name="people" />
            Membros
          </a>
        </div>
      </header>

      {map === undefined ? (
        <Loading label="A carregar o mapa…" />
      ) : map === 'signin' ? (
        <div className="notice" role="status">
          <p>O mapa de membros é da área de membros.</p>
          <a className="btn btn--primary btn--sm" href={loginTo(portal.membersMap)}>
            <Icon name="login" />
            Entrar
          </a>
        </div>
      ) : map === null ? (
        <div className="notice" role="status">
          <p>Não foi possível carregar o mapa. Tenta outra vez daqui a pouco.</p>
        </div>
      ) : (
        <>
          <LeafletMap cities={map.cities} focus={focus} />
          {map.cities.length === 0 && (
            <p className="note map-page__empty">
              {map.pending.length > 0
                ? 'Ainda nenhuma cidade está localizada; as que estão em espera aparecem em breve.'
                : 'Nenhum membro indicou a cidade no perfil.'}
            </p>
          )}
          <ul className="map-stats" aria-label="Resumo">
            <li>
              <strong>{map.total}</strong> membros
            </li>
            <li>
              <strong>{map.cities.length}</strong> {map.cities.length === 1 ? 'cidade' : 'cidades'}
            </li>
            <li>
              <strong>{map.withoutCity.length}</strong> sem cidade
            </li>
            {map.pending.length > 0 && (
              <li>
                <strong>{map.pending.length}</strong> à espera de localização
              </li>
            )}
          </ul>
          <div className="map-lists">
            {map.cities.length > 0 && (
              <details className="map-list" open>
                <summary>Cidades</summary>
                <ul>
                  {map.cities.map((c) => (
                    <li key={c.name}>
                      <button type="button" className="map-city" onClick={() => setFocus(c)}>
                        <Icon name="geo" />
                        <span>{c.name}</span>
                        <span className="map-city__count">{c.members.length}</span>
                      </button>
                    </li>
                  ))}
                </ul>
              </details>
            )}
            {map.withoutCity.length > 0 && (
              <details className="map-list">
                <summary>Sem cidade no perfil</summary>
                <ul className="member-rows">
                  {map.withoutCity.map((m, i) => (
                    <li key={i} className="member-row">
                      <MemberFace avatarUrl={m.avatarUrl} size={36} />
                      <span className="member-row__who">
                        <strong>{m.displayName}</strong>
                        {m.fullName && <small>{m.fullName}</small>}
                      </span>
                    </li>
                  ))}
                </ul>
              </details>
            )}
            {map.pending.length > 0 && (
              <details className="map-list">
                <summary>À espera de localização</summary>
                <ul>
                  {map.pending.map((c) => (
                    <li key={c} className="map-pending">
                      {c}
                    </li>
                  ))}
                </ul>
                <p className="note">Estas cidades são localizadas em segundo plano; volta a abrir o mapa daqui a uns minutos.</p>
              </details>
            )}
          </div>
        </>
      )}
    </section>
  );
}

/** The map itself: dark CARTO tiles, one numbered marker per city, a popup with up to ten members. */
function LeafletMap({ cities, focus }: { cities: MapCity[]; focus?: MapCity }) {
  const box = useRef<HTMLDivElement>(null);
  const leaflet = useRef<L.Map | undefined>(undefined);
  const markers = useRef(new Map<string, L.Marker>());

  useEffect(() => {
    if (!box.current) return;
    const map = L.map(box.current, { preferCanvas: true }).setView(PORTUGAL, 7);
    L.tileLayer(TILES, { attribution: ATTRIBUTION, subdomains: 'abcd', maxZoom: 19 }).addTo(map);
    for (const city of cities) {
      const marker = L.marker([city.latitude, city.longitude], {
        icon: L.divIcon({ className: 'map-marker', html: markerHtml(city.members.length), iconSize: [30, 42], iconAnchor: [15, 42], popupAnchor: [0, -42] }),
        title: city.name,
        keyboard: true,
      })
        .bindPopup(() => popup(city), { maxWidth: 280, className: 'map-popup' })
        .addTo(map);
      markers.current.set(city.name, marker);
    }
    if (cities.length > 0) map.fitBounds(L.latLngBounds(cities.map((c) => [c.latitude, c.longitude] as L.LatLngTuple)), { padding: [40, 40], maxZoom: 11 });
    leaflet.current = map;
    const known = markers.current;
    return () => {
      map.remove();
      known.clear();
    };
  }, [cities]);

  useEffect(() => {
    if (!focus || !leaflet.current) return;
    leaflet.current.setView([focus.latitude, focus.longitude], Math.max(leaflet.current.getZoom(), 10));
    markers.current.get(focus.name)?.openPopup();
    box.current?.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
  }, [focus]);

  return <div ref={box} className="map-canvas" role="region" aria-label="Mapa com os membros por cidade" />;
}

/** The marker's count, digits only (no member data goes into markup strings). */
const markerHtml = (count: number) => `<span class="map-marker__pin"></span><span class="map-marker__count">${Math.trunc(count)}</span>`;

/** The popup as DOM nodes: names go in as text, never as markup. */
function popup(city: MapCity) {
  const root = document.createElement('div');
  root.className = 'map-popup__body';
  const title = root.appendChild(document.createElement('p'));
  title.className = 'map-popup__city';
  title.textContent = city.name;
  const count = root.appendChild(document.createElement('p'));
  count.className = 'map-popup__count';
  count.textContent = `${city.members.length} ${city.members.length === 1 ? 'membro' : 'membros'}`;
  const list = root.appendChild(document.createElement('ul'));
  list.className = 'map-popup__list';
  for (const member of city.members.slice(0, POPUP_LIMIT)) list.appendChild(person(member));
  if (city.members.length > POPUP_LIMIT) {
    const more = list.appendChild(document.createElement('li'));
    more.className = 'map-popup__more';
    const rest = city.members.length - POPUP_LIMIT;
    more.textContent = `e mais ${rest} ${rest === 1 ? 'membro' : 'membros'}`;
  }
  return root;
}

function person(member: Person) {
  const item = document.createElement('li');
  const img = item.appendChild(document.createElement('img'));
  img.src = member.avatarUrl ?? DEFAULT_AVATAR;
  img.alt = '';
  img.width = 36;
  img.height = 36;
  img.loading = 'lazy';
  img.addEventListener('error', () => {
    if (!img.src.endsWith(DEFAULT_AVATAR)) img.src = DEFAULT_AVATAR;
  });
  const who = item.appendChild(document.createElement('span'));
  const name = who.appendChild(document.createElement('strong'));
  name.textContent = member.displayName;
  if (member.fullName) {
    const full = who.appendChild(document.createElement('small'));
    full.textContent = member.fullName;
  }
  return item;
}
