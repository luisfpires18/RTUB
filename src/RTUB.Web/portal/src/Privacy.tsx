import { useEffect } from 'react';
import { portal } from './content';
import { Icon } from './icons';

// The legal source of the Privacy Policy served at /privacy, carried verbatim from the retired Blazor
// Pages/Public/Privacy.razor (React track 004). Edit it as legal text, not as portal copy.
const toc = [
  'Introdução',
  'Dados Recolhidos',
  'Finalidade do Tratamento de Dados',
  'Partilha de Dados com Terceiros',
  'Proteção dos Dados',
  'Direitos dos Titulares dos Dados',
  'Retenção de Dados',
  'Contacto',
  'Alterações à Política de Privacidade',
];

export default function Privacy() {
  useEffect(() => {
    document.title = 'Política de Privacidade · RTUB';
  }, []);

  return (
    <article className="doc wrap" aria-labelledby="privacy-title">
      <header className="doc__head">
        <a className="back-link" href={portal.home}>
          <Icon name="arrow" />
          Portal RTUB
        </a>
        <h1 id="privacy-title" className="doc__title">
          Política de Privacidade
        </h1>
        <p className="doc__meta">Última atualização: Dezembro de 2025</p>
      </header>

      <nav className="doc__toc" aria-label="Índice">
        {/* Open beside the text on wide screens; collapsed above it on phones. */}
        <details open={matchMedia('(min-width: 960px)').matches}>
          <summary>Índice</summary>
          <ol>
            {toc.map((t, i) => (
              <li key={t}>
                <a href={`#s${i + 1}`}>{t}</a>
              </li>
            ))}
          </ol>
        </details>
      </nav>

      <div className="doc__body">
        <section id="s1">
          <h2>1. Introdução</h2>
          <p>
            A Real Tuna Universitária de Bragança (RTUB) está comprometida em proteger a privacidade dos seus membros,
            visitantes e utilizadores da plataforma. Esta política de privacidade descreve como recolhemos, utilizamos e
            protegemos os seus dados pessoais.
          </p>
        </section>

        <section id="s2">
          <h2>2. Dados Recolhidos</h2>
          <p>A RTUB recolhe os seguintes tipos de informação:</p>
          <ul>
            <li>
              <strong>Dados de Identificação:</strong> Nome, alcunha (nickname), data de nascimento
            </li>
            <li>
              <strong>Dados de Contacto:</strong> Endereço de email, número de telefone
            </li>
            <li>
              <strong>Dados de Autenticação:</strong> Nome de utilizador, palavra-passe (encriptada)
            </li>
            <li>
              <strong>Dados de Perfil:</strong> Fotografia de perfil, informações sobre categoria de membro, instrumentos
              musicais
            </li>
            <li>
              <strong>Dados de Atividade:</strong> Participação em ensaios, eventos, reuniões e outras atividades
            </li>
            <li>
              <strong>Dados de Utilização:</strong> Informações sobre a utilização da plataforma para estatísticas
              internas
            </li>
          </ul>
        </section>

        <section id="s3">
          <h2>3. Finalidade do Tratamento de Dados</h2>
          <p>Os dados recolhidos são utilizados exclusivamente para:</p>
          <ul>
            <li>
              <strong>Gestão da Plataforma:</strong> Administração de contas de utilizador e acesso aos recursos
            </li>
            <li>
              <strong>Autenticação e Segurança:</strong> Verificação de identidade e proteção de contas
            </li>
            <li>
              <strong>Comunicação:</strong> Envio de notificações sobre atividades, ensaios, eventos e outras informações
              relevantes
            </li>
            <li>
              <strong>Organização de Atividades:</strong> Planeamento e gestão de ensaios, eventos, reuniões e outras
              atividades da RTUB
            </li>
            <li>
              <strong>Estatísticas Internas:</strong> Análise do uso da plataforma para melhorar os serviços oferecidos
            </li>
            <li>
              <strong>Gestão Administrativa:</strong> Manutenção de registos de membros e gestão interna da organização
            </li>
          </ul>
        </section>

        <section id="s4">
          <h2>4. Partilha de Dados com Terceiros</h2>
          <p>
            A RTUB <strong>não partilha</strong> os seus dados pessoais com terceiros para fins comerciais ou
            publicitários. Os dados podem ser partilhados apenas com:
          </p>
          <ul>
            <li>
              <strong>Fornecedores de Infraestrutura:</strong> Microsoft Azure e outros serviços de alojamento e
              tecnologia essenciais para o funcionamento da plataforma
            </li>
            <li>
              <strong>Autoridades Competentes:</strong> Quando legalmente obrigados ou para proteção de direitos legais
            </li>
          </ul>
          <p>
            Todos os fornecedores de serviços são cuidadosamente selecionados e obrigados a cumprir com as normas de
            proteção de dados aplicáveis.
          </p>
        </section>

        <section id="s5">
          <h2>5. Proteção dos Dados</h2>
          <p>
            A RTUB implementa medidas técnicas e organizacionais para proteger os seus dados pessoais, incluindo:
          </p>
          <ul>
            <li>
              <strong>Encriptação:</strong> As palavras-passe são armazenadas de forma encriptada
            </li>
            <li>
              <strong>Acesso Restrito:</strong> Apenas membros associados efetivos e autorizados têm acesso a dados
              pessoais
            </li>
            <li>
              <strong>Comunicações Seguras:</strong> Utilização de HTTPS para todas as comunicações na plataforma
            </li>
            <li>
              <strong>Backups Regulares:</strong> Cópias de segurança para prevenir perda de dados
            </li>
            <li>
              <strong>Auditoria:</strong> Monitorização regular dos sistemas para detetar e prevenir acessos não
              autorizados
            </li>
          </ul>
        </section>

        <section id="s6">
          <h2>6. Direitos dos Titulares dos Dados</h2>
          <p>De acordo com o Regulamento Geral de Proteção de Dados (RGPD), tem os seguintes direitos:</p>
          <ul>
            <li>
              <strong>Direito de Acesso:</strong> Pode solicitar uma cópia dos seus dados pessoais
            </li>
            <li>
              <strong>Direito de Retificação:</strong> Pode solicitar a correção de dados incorretos ou incompletos
            </li>
            <li>
              <strong>Direito ao Apagamento:</strong> Pode solicitar a eliminação dos seus dados pessoais
            </li>
            <li>
              <strong>Direito à Limitação do Tratamento:</strong> Pode solicitar a restrição do tratamento dos seus
              dados
            </li>
            <li>
              <strong>Direito à Portabilidade:</strong> Pode solicitar a transferência dos seus dados para outro
              responsável
            </li>
            <li>
              <strong>Direito de Oposição:</strong> Pode opor-se ao tratamento dos seus dados em determinadas
              circunstâncias
            </li>
          </ul>
        </section>

        <section id="s7">
          <h2>7. Retenção de Dados</h2>
          <p>
            Os dados pessoais são conservados pelo tempo necessário para cumprir as finalidades para as quais foram
            recolhidos, ou conforme exigido por lei. Após o término da relação com a RTUB, os dados podem ser mantidos
            para fins históricos e estatísticos, ou eliminados mediante solicitação.
          </p>
        </section>

        <section id="s8">
          <h2>8. Contacto</h2>
          <p>
            Para exercer os seus direitos ou esclarecer qualquer dúvida sobre a proteção dos seus dados pessoais, pode
            contactar-nos através do seguinte endereço de email:
          </p>
          <p className="doc__contact">
            <strong>Email:</strong> <a href="mailto:realtunab@gmail.com">realtunab@gmail.com</a>
          </p>
          <p>A RTUB compromete-se a responder a todos os pedidos no prazo de 30 dias.</p>
        </section>

        <section id="s9">
          <h2>9. Alterações à Política de Privacidade</h2>
          <p>
            A RTUB reserva-se o direito de atualizar esta política de privacidade a qualquer momento. Quaisquer
            alterações serão publicadas nesta página e, se forem significativas, os utilizadores serão notificados.
          </p>
          <p>
            <strong>Última atualização:</strong> Dezembro de 2025
          </p>
        </section>
      </div>
    </article>
  );
}
