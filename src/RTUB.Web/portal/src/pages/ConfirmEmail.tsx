import { useEffect, useState } from 'react';
import { PageHead } from '../components/ui/PageHead';
import { accountApi, takeLinkQuery, type RecoveryResult } from '../data/accountApi';
import { portal } from '../data/content';
import { Icon } from '../components/icons';
import { Loading } from '../components/ui/Loading';
import { AccountDone } from '../components/ui/AccountDone';

// The emailed link: /confirm-email?userId=…&code=…. Read once, then gone from the address bar.
const link = takeLinkQuery('userId', 'code');
let confirming: ReturnType<typeof accountApi.confirmEmail> | undefined;

/**
 * /confirm-email. Confirms the address once, on opening the link. It
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
      <PageHead plain eyebrow="Área de membros" title="Confirmação de email" titleId="confirm-title" />

      <div className="request__form-card account-card">
        {!result ? (
          <Loading label="A processar a confirmação…" />
        ) : (
          <AccountDone
            error={!result.succeeded}
            role={result.succeeded ? 'status' : 'alert'}
            icon={result.succeeded ? 'checkCircle' : 'warning'}
            message={result.message}
          >
            {result.succeeded && (
              <a className="btn btn--primary" href={portal.login}>
                <Icon name="login" />
                Entrar
              </a>
            )}
          </AccountDone>
        )}
      </div>
    </section>
  );
}
