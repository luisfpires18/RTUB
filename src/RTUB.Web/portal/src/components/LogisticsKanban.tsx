// The Kanban columns of a board page, with drag and drop.
import type { DragEvent } from 'react';
import { Icon } from './icons';
import { logisticsApi, STATUS, type Board, type Card, type List, type Status } from '../data/logisticsApi';
import type { Drag, Drop, Open } from '../data/logisticsBoardTypes';
import { Menu } from './LogisticsMenu';
import { CardFace } from './LogisticsCardFace';

/** The Kanban columns of a board, with drag and drop. */
export function LogisticsKanban({
  data, visible, drag, drop, setDrag, setDrop, dragged, columns, filtering, manage, archived, states, run, setOpen, onDragOver, onDrop,
}: {
  data: Board; visible: (cards: Card[]) => Card[]; drag?: Drag; drop?: Drop; setDrag: (d: Drag | undefined) => void; setDrop: (d: Drop | undefined) => void;
  dragged: { current: Drag | undefined }; columns: { current: Record<number, HTMLElement | null> }; filtering: boolean; manage: boolean; archived: boolean;
  states: Status[]; run: (o: Promise<{ kind: string }>, what: string) => Promise<void>; setOpen: (o: Open | undefined) => void;
  onDragOver: (e: DragEvent<HTMLElement>, list: List) => void; onDrop: (e: DragEvent<HTMLElement>, list: List) => void;
}) {
  return (
    <div className="lx-kanban" aria-label="Listas do quadro">
      {data.lists.map((list, li) => {
        const cards = visible(list.cards);
        const others = cards.filter((c) => c.id !== drag?.cardId);
        return (
          <section
            key={list.id}
            ref={(el) => {
              columns.current[list.id] = el;
            }}
            className={drop?.listId === list.id ? 'lx-col lx-col--over' : 'lx-col'}
            aria-label={list.name}
          >
            <header className="lx-col__head">
              <h2 className="lx-col__title">{list.name}</h2>
              <span className={`lx-status lx-view__status--${list.state.toLowerCase()}`} title={`Cartões nesta coluna: ${STATUS[list.state].name}`}>
                {STATUS[list.state].label}
              </span>
              <span className="lx-col__count">{filtering ? `${cards.length}/${list.cards.length}` : list.cards.length}</span>
              {manage && (
                <Menu label={`Ações da lista ${list.name}`}>
                  {(close) => (
                    <>
                      <button type="button" role="menuitem" onClick={() => (close(), setOpen({ kind: 'list', list }))}>
                        <Icon name="pencil" />
                        Nome e estado
                      </button>
                      <button type="button" role="menuitem" disabled={li === 0} onClick={() => (close(), run(logisticsApi.moveList(list.id, li - 1), 'mover a lista'))}>
                        <Icon name="prev" />
                        Mover para a esquerda
                      </button>
                      <button
                        type="button"
                        role="menuitem"
                        disabled={li === data.lists.length - 1}
                        onClick={() => (close(), run(logisticsApi.moveList(list.id, li + 1), 'mover a lista'))}
                      >
                        <Icon name="next" />
                        Mover para a direita
                      </button>
                      <button type="button" role="menuitem" className="lx-menu__danger" onClick={() => (close(), setOpen({ kind: 'delete-list', list }))}>
                        <Icon name="trash" />
                        Eliminar coluna
                      </button>
                    </>
                  )}
                </Menu>
              )}
            </header>
            <ol
              className="lx-col__cards"
              onDragOver={(e) => onDragOver(e, list)}
              onDrop={(e) => onDrop(e, list)}
              onDragLeave={(e) => {
                if (!e.currentTarget.contains(e.relatedTarget as Node)) setDrop(undefined);
              }}
            >
              {cards.map((card) => (
                <li key={card.id} className="lx-col__slot">
                  {drop?.listId === list.id && card.id !== drag?.cardId && drop.index === others.indexOf(card) && (
                    <span className="lx-drop" aria-hidden="true" />
                  )}
                  <CardFace
                    card={card}
                    canManage={manage}
                    canMove={!archived && (data.canManage || card.mine)}
                    dragging={drag?.cardId === card.id}
                    onOpen={() => setOpen({ kind: 'card', id: card.id })}
                    onDragStart={(e) => {
                      e.dataTransfer.effectAllowed = 'move';
                      e.dataTransfer.setData('text/plain', String(card.id));
                      dragged.current = { cardId: card.id, fromList: list.id };
                      setDrag(dragged.current);
                    }}
                    onDragEnd={() => {
                      dragged.current = undefined;
                      setDrag(undefined);
                      setDrop(undefined);
                    }}
                    menu={(close) => (
                      <>
                        {states
                          .filter((s) => s !== card.status)
                          .map((s) => (
                            <button key={s} type="button" role="menuitem" onClick={() => (close(), run(logisticsApi.setStatus(card.id, s), 'mudar o estado'))}>
                              <span className={`lx-dot lx-dot--${s.toLowerCase()}`} aria-hidden="true" />
                              Marcar {STATUS[s].label}
                            </button>
                          ))}
                        <button type="button" role="menuitem" onClick={() => (close(), setOpen({ kind: 'move', card, listId: list.id }))}>
                          <Icon name="arrow" />
                          Mover…
                        </button>
                        {manage && (
                          <>
                            <button type="button" role="menuitem" onClick={() => (close(), run(logisticsApi.duplicateCard(card.id), 'duplicar o cartão'))}>
                              <Icon name="kanban" />
                              Duplicar
                            </button>
                            <button type="button" role="menuitem" onClick={() => (close(), run(logisticsApi.archiveCard(card.id, true), 'arquivar o cartão'))}>
                              <Icon name="folder" />
                              Arquivar
                            </button>
                            <button type="button" role="menuitem" className="lx-menu__danger" onClick={() => (close(), setOpen({ kind: 'delete-card', card }))}>
                              <Icon name="trash" />
                              Eliminar cartão
                            </button>
                          </>
                        )}
                      </>
                    )}
                  />
                </li>
              ))}
              {drop?.listId === list.id && drop.index >= others.length && (
                <li className="lx-col__slot" aria-hidden="true">
                  <span className="lx-drop" />
                </li>
              )}
              {cards.length === 0 && !drop && <li className="lx-col__empty">{filtering ? 'Nada com estes filtros.' : 'Sem cartões.'}</li>}
            </ol>
            {manage && (
              <button type="button" className="lx-col__add" onClick={() => setOpen({ kind: 'create-card', list })}>
                <Icon name="plus" />
                Adicionar cartão
              </button>
            )}
          </section>
        );
      })}
      {manage && (
        <button type="button" className="lx-col lx-col--new" onClick={() => setOpen({ kind: 'list' })}>
          <Icon name="plus" />
          Adicionar coluna
        </button>
      )}
    </div>
  );
}
