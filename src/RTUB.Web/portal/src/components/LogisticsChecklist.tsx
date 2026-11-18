// Card checklist panel.
import { useId, useState } from 'react';
import { Icon } from './icons';
import { Section, type Apply } from './LogisticsPanel';
import { logisticsApi, type CardDetail } from '../data/logisticsApi';
import { FieldError } from './ui/FieldError';

/** One item at a time: tick, rename (Enter saves, Esc cancels), remove, add. The item's text identifies it on the server. */
export function Checklist({ detail, busy, errors, apply }: { detail: CardDetail; busy?: string; errors: Record<string, string>; apply: Apply }) {
  const [task, setTask] = useState('');
  const [editing, setEditing] = useState<{ index: number; text: string }>();
  const id = useId();
  const items = detail.checklist;
  const done = items.filter((i) => i.done).length;
  const cardId = detail.card.id;

  const add = async () => {
    if (task.trim() && (await apply(logisticsApi.addItem(cardId, task.trim()), 'task', 'adicionar a tarefa'))) setTask('');
  };
  const rename = async () => {
    if (!editing) return;
    const item = items[editing.index];
    if (!editing.text.trim() || editing.text.trim() === item.task) return setEditing(undefined);
    if (await apply(logisticsApi.renameItem(cardId, editing.index, item.task, editing.text.trim()), 'items', 'editar a tarefa')) setEditing(undefined);
  };

  return (
    <Section id={`${id}-check`} title="Checklist" extra={items.length > 0 && <small>{done}/{items.length}</small>}>
      {items.length === 0 ? (
        <p className="note">{detail.canWork ? 'Sem tarefas. Divida o trabalho em passos abaixo.' : 'Sem tarefas.'}</p>
      ) : (
        <>
          <span className="lx-progress__bar lx-progress__bar--wide">
            <span style={{ width: `${Math.round((done / items.length) * 100)}%` }} />
          </span>
          <ul className="lx-checklist">
            {items.map((item, i) =>
              !detail.canWork ? (
                <li key={i} className="lx-checklist__read">
                  {item.done ? <Icon name="checkCircle" className="lx-checklist__tick" /> : <span className="lx-checklist__open" aria-hidden="true" />}
                  <span className={item.done ? 'lx-checklist__done' : undefined}>
                    {item.task}
                    <span className="sr-only">{item.done ? ' (feita)' : ' (por fazer)'}</span>
                  </span>
                </li>
              ) : editing?.index === i ? (
                <li key={i}>
                  <input
                    className="lx-checklist__edit"
                    type="text"
                    aria-label={`Editar a tarefa ${item.task}`}
                    maxLength={200}
                    autoFocus
                    value={editing.text}
                    onChange={(e) => setEditing({ index: i, text: e.target.value })}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter') (e.preventDefault(), rename());
                      if (e.key === 'Escape') (e.preventDefault(), e.stopPropagation(), setEditing(undefined));
                    }}
                  />
                  <button type="button" className="icon-btn icon-btn--sm" onClick={rename} disabled={!!busy}>
                    <Icon name="check" />
                    <span className="sr-only">Guardar a tarefa</span>
                  </button>
                </li>
              ) : (
                <li key={i}>
                  <label>
                    <input
                      type="checkbox"
                      checked={item.done}
                      disabled={!!busy}
                      onChange={() => apply(logisticsApi.toggleItem(cardId, i, item.task, !item.done), 'items', 'guardar a tarefa')}
                    />
                    <span className={item.done ? 'lx-checklist__done' : undefined}>{item.task}</span>
                  </label>
                  <button type="button" className="icon-btn icon-btn--sm" onClick={() => setEditing({ index: i, text: item.task })}>
                    <Icon name="pencil" />
                    <span className="sr-only">Editar {item.task}</span>
                  </button>
                  <button
                    type="button"
                    className="icon-btn icon-btn--sm"
                    disabled={!!busy}
                    onClick={() => apply(logisticsApi.removeItem(cardId, i, item.task), 'items', 'remover a tarefa')}
                  >
                    <Icon name="trash" />
                    <span className="sr-only">Remover {item.task}</span>
                  </button>
                </li>
              ),
            )}
          </ul>
        </>
      )}
      <FieldError text={errors.items} />
      {detail.canWork && (
        <div className="lx-inline">
          <input
            type="text"
            aria-label="Nova tarefa"
            placeholder="Nova tarefa"
            maxLength={200}
            value={task}
            onChange={(e) => setTask(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && (e.preventDefault(), add())}
          />
          <button type="button" className="btn btn--ghost btn--sm" disabled={!task.trim() || !!busy} onClick={add}>
            <Icon name="plus" />
            Adicionar
          </button>
        </div>
      )}
      <FieldError text={errors.task} />
    </Section>
  );
}
