// Card labels panel.
import { useId, useState } from 'react';
import { Icon } from './icons';
import { Section, type Apply } from './LogisticsPanel';
import { logisticsApi, type CardDetail } from '../data/logisticsApi';
import { FieldError } from './ui/FieldError';

const HEX = /^#[0-9a-f]{6}$/i;

export function Labels({ detail, apply, errors }: { detail: CardDetail; apply: Apply; errors: Record<string, string> }) {
  const [text, setText] = useState('');
  const [colour, setColour] = useState('#6f42c1');
  const id = useId();
  const labels = detail.card.labels;
  const cardId = detail.card.id;
  if (!detail.canManage && labels.length === 0) return null;

  const add = async () => {
    if (text.trim() && (await apply(logisticsApi.setLabels(cardId, [...labels, { text: text.trim(), color: colour }]), 'labels', 'guardar as etiquetas'))) setText('');
  };

  return (
    <Section id={`${id}-labels`} title="Etiquetas">
      {labels.length > 0 ? (
        <ul className="lx-labels">
          {labels.map((l, i) => (
            <li key={`${l.text}-${i}`} className={detail.canManage ? 'lx-label lx-label--edit' : 'lx-label'}>
              <span className="lx-label__dot" style={{ background: HEX.test(l.color) ? l.color : '#6f42c1' }} aria-hidden="true" />
              {l.text}
              {detail.canManage && (
                <button type="button" className="chip__remove" onClick={() => apply(logisticsApi.setLabels(cardId, labels.filter((_, j) => j !== i)), 'labels', 'guardar as etiquetas')}>
                  <Icon name="close" />
                  <span className="sr-only">Remover {l.text}</span>
                </button>
              )}
            </li>
          ))}
        </ul>
      ) : (
        <p className="note">Sem etiquetas.</p>
      )}
      {detail.canManage && (
        <div className="lx-inline">
          <input type="text" aria-label="Nome da etiqueta" placeholder="Nova etiqueta" maxLength={50} value={text} onChange={(e) => setText(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && (e.preventDefault(), add())} />
          <input type="color" aria-label="Cor da etiqueta" value={colour} onChange={(e) => setColour(e.target.value)} />
          <button type="button" className="btn btn--ghost btn--sm" onClick={add} disabled={!text.trim()}>
            <Icon name="plus" />
            Adicionar
          </button>
        </div>
      )}
      <FieldError text={errors.labels} />
    </Section>
  );
}
