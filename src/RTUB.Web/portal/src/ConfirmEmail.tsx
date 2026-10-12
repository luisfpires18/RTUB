import { useEffect, useState } from 'react';
import { accountApi, takeLinkQuery, type RecoveryResult } from './accountApi';
import { Loading } from './App';
import { portal } from './content';
import { Icon } from './icons';

// The emailed link: /confirm-email?userId=…&code=…. Read once, then gone from the address bar.
const link = takeLinkQuery('userId', 'code');
let confirming: ReturnType<typeof accountApi.confirmEmail> | undefined;

/**
 * /confirm-email (task 036; was a Blazor page). Confirms the address once, as the old page did on opening the link. It
 * does not sign anyone in.
 */
export default function ConfirmEmail() {
  const [result, setResult] = useState<RecoveryResult>();

  useEffect(() => {
    document.title = 'Confirmação de email · RTUB';
    // One request per page load, even when React mounts twice in development.
    confirming ??= accountApi.confirmEmail(link.userId, link.code);
    confirming.then((o) =>
      setResult(o.kind === 'ok' ? o.data : { succeeded: false, message: 'Não foi possível confirmar o email agora. Tenta de novo daqui a pouco.' }),
    );
  }, []);

  return (
    <section className="page wrap" aria-labelledby="confirm-title">
      <header className="page__head">
        <p className="eyebrow">Área de membros</p>
        <h1 id="confirm-title" className="page__title">
          Confirmação de email
        </h1>
      </header>

      <div className="request__form-card account-card">
        {!result ? (
          <Loading label="A processar a confirmação…" />
        ) : (
          <div className={result.succeeded ? 'account-done' : 'account-done account-done--error'} role={result.succeeded ? 'status' : 'alert'}>
            <Icon name={result.succeeded ? 'checkCircle' : 'warning'} />
            <p>{result.message}</p>
            {result.succeeded && (
              <a className="btn btn--primary" href={portal.login}>
                <Icon name="login" />
                Entrar
              </a>
            )}
          </div>
        )}
      </div>
    </section>
  );
}
