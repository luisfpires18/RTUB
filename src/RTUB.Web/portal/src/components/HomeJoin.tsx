// Home page join-us, request/members doors and app install sections.
import { AccountLink, ExternalLink } from './AppShared';
import { contactEmail, playStoreUrl, portal } from '../data/content';
import { Icon } from './icons';
import { SectionHead } from './HomeParts';
import { Skyline } from './HomeHero';

// ---------- join ----------

export function JoinUs() {
  return (
    <section id="join" className="section join" aria-labelledby="join-title">
      <div className="wrap split">
        <div>
          <SectionHead id="join-title" eyebrow="Novos elementos" title="Há sempre lugar para mais uma voz">
            Estudas na UPB e gostas de música, de noites longas e de boa companhia? Vem a um ensaio e conhece a
            tuna por dentro.
          </SectionHead>
          <p className="join__quote">Ninguém nasce a tocar instrumentos. Aprende-se aqui.</p>
        </div>
        <dl className="join__facts">
          <div>
            <dt>
              <Icon name="clock" />
              Ensaios
            </dt>
            <dd>Às terças e quintas, das 21h00 à meia-noite.</dd>
          </div>
          <div>
            <dt>
              <Icon name="geo" />
              Local
            </dt>
            <dd>Quinta de Santa Apolónia, no campus da Universidade Politécnica de Bragança.</dd>
          </div>
          <div>
            <dt>
              <Icon name="arrow" />
              Primeiro passo
            </dt>
            <dd>Basta aparecer. Se preferires, fala connosco antes pelo Instagram ou pelo Facebook.</dd>
          </div>
          <div>
            <dt>
              <Icon name="person" />
              Percurso
            </dt>
            <dd>Entra-se como Leitão, passa-se a Caloiro e depois a Tuno; os cordões do traje mostram cada etapa.</dd>
          </div>
        </dl>
      </div>
    </section>
  );
}

// ---------- request + members ----------

export function Doors() {
  return (
    <section id="request" className="section" aria-labelledby="request-title">
      <div className="wrap doors">
        <div className="door door--request">
          <p className="eyebrow">Pedidos</p>
          <h2 id="request-title" className="section__title">
            Leve a RTUB ao seu evento
          </h2>
          <p className="door__lead">
            Serenatas, casamentos, batizados, aniversários, arraiais ou festivais. Conte-nos o que tem em mente.
          </p>
          <ol className="steps">
            <li>
              <strong>Preencha o pedido</strong> com a data, o local e o tipo de evento.
            </li>
            <li>
              <strong>Vemos a disponibilidade</strong> e damos-lhe uma resposta.
            </li>
            <li>
              <strong>No dia combinado,</strong> a RTUB leva a música até si.
            </li>
          </ol>
          <div className="door__actions">
            <a className="btn btn--light btn--lg" href={portal.request}>
              <Icon name="send" />
              Fazer um pedido
            </a>
            <a className="door__mail" href={`mailto:${contactEmail}`}>
              <Icon name="envelope" />
              {contactEmail}
            </a>
          </div>
        </div>
        <div className="door door--members">
          <p className="eyebrow">Área de membros</p>
          <h2 className="door__title">Só para a tuna</h2>
          <p className="door__lead">
            Ensaios, atuações e o dia a dia dos membros da RTUB. O acesso é criado pela própria tuna; não há registo
            público.
          </p>
          <AccountLink className="member-link" signedOutLabel="Entrar como membro" />
          <Skyline patternId="merlons-members" />
        </div>
      </div>
    </section>
  );
}

// ---------- install ----------

export function InstallApp() {
  return (
    <section id="app" className="section section--raise" aria-labelledby="app-title">
      <div className="wrap">
        <SectionHead id="app-title" eyebrow="No telemóvel" title="Instalar a app">
          A RTUB também vive no ecrã principal do telemóvel. Escolhe o caminho do teu aparelho.
        </SectionHead>
        <ol className="install">
          <li className="install__step">
            <Icon name="googlePlay" className="install__icon" />
            <h3 className="install__title">Android</h3>
            <p>Instala a app RTUB a partir da Google Play.</p>
            <ExternalLink href={playStoreUrl} className="more">
              Abrir na Google Play
            </ExternalLink>
          </li>
          <li className="install__step">
            <Icon name="apple" className="install__icon" />
            <h3 className="install__title">iPhone e iPad</h3>
            <p>
              Abre este site no Safari, toca em <strong>Partilhar</strong> e escolhe{' '}
              <strong>Adicionar ao ecrã principal</strong>.
            </p>
          </li>
          <li className="install__step">
            <Icon name="phone" className="install__icon" />
            <h3 className="install__title">Android, pelo navegador</h3>
            <p>
              No Chrome, abre o menu <strong>⋮</strong> e escolhe <strong>Instalar app</strong> ou{' '}
              <strong>Adicionar ao ecrã principal</strong>, se a opção aparecer.
            </p>
          </li>
          <li className="install__step">
            <Icon name="laptop" className="install__icon" />
            <h3 className="install__title">Computador</h3>
            <p>No Chrome ou no Edge, o ícone de instalar surge na barra de endereço quando o navegador o permite.</p>
          </li>
        </ol>
      </div>
    </section>
  );
}
