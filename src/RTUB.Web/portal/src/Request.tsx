import { useEffect } from 'react';
import { contactEmail, legacy } from './content';
import { Icon } from './icons';

// Examples only: the request form takes the event type as free text. Values follow the EventType
// enum, minus the internal "Nerba".
const examples = ['Serenata', 'Casamento', 'Batizado', 'Aniversário', 'Arraial', 'Arruada', 'Convívio', 'Missa', 'Festival'];

/**
 * /portal/request - prepares a request and hands off to the Blazor /request form, which still owns
 * submission (persistence, admin push and the email to RTUB). The React form arrives with the
 * POST endpoint planned in docs/react-portal-pilot.md; nothing here pretends to submit.
 */
export default function RequestPage() {
  useEffect(() => {
    document.title = 'Pedir uma atuação · RTUB';
  }, []);

  return (
    <section className="page wrap" aria-labelledby="request-page-title">
      <header className="page__head">
        <p className="eyebrow">Pedidos</p>
        <h1 id="request-page-title" className="page__title">
          Pedir uma atuação
        </h1>
        <p className="page__lead">
          Uma serenata à janela, a entrada dos noivos, a festa da aldeia: diga-nos o que imagina e combinamos os
          detalhes consigo.
        </p>
      </header>

      <div className="request">
        <div className="request__prep">
          <h2 className="request__title">Tenha à mão</h2>
          <ul className="checklist">
            <li>
              <Icon name="check" />O seu nome, email e telefone
            </li>
            <li>
              <Icon name="check" />O tipo de evento
            </li>
            <li>
              <Icon name="check" />A data, ou um intervalo de datas se ainda estiver por decidir
            </li>
            <li>
              <Icon name="check" />O local
            </li>
            <li>
              <Icon name="check" />O que ajudar a preparar a atuação: horário, duração, número de pessoas
            </li>
          </ul>

          <h2 className="request__title">Alguns exemplos</h2>
          <ul className="chips" aria-label="Exemplos de eventos">
            {examples.map((e) => (
              <li key={e} className="chip">
                {e}
              </li>
            ))}
          </ul>
        </div>

        <aside className="request__go" aria-labelledby="request-go-title">
          <h2 id="request-go-title" className="request__title">
            Pronto para avançar?
          </h2>
          <p>
            Cada pedido chega diretamente à RTUB, que confirma a disponibilidade e responde pelo contacto indicado.
          </p>
          <a className="btn btn--light btn--lg" href={legacy.request}>
            <Icon name="send" />
            Preencher o pedido
          </a>
          <a className="door__mail" href={`mailto:${contactEmail}`}>
            <Icon name="envelope" />
            {contactEmail}
          </a>
          <p className="note">Por agora, o pedido é concluído na página atual da RTUB.</p>
        </aside>
      </div>
    </section>
  );
}
