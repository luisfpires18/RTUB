import { portal } from '../data/content';
import { ownerApi } from '../data/ownerApi';
import { OwnerPage, useOwnerLoad } from '../components/OwnerUi';
import { Compose } from '../components/OwnerEmailCompose';
import { Recipients } from '../components/OwnerEmailRecipients';

/**
 * /emails: the Owner writes an announcement and sends it by email to
 * every account with a confirmed address and "Notificações por email" on, and chooses who those are. The server decides
 * the recipients when it sends and refuses anyone but the Owner.
 */
export default function OwnerEmails() {
  const [recipients, , setRecipients] = useOwnerLoad(() => ownerApi.emailRecipients(), []);

  return (
    <OwnerPage
      path={portal.emails}
      title="Emails"
      lead="Enviar um anúncio por email a todos os utilizadores subscritos."
      icon="envelope"
      state={recipients}
      loadingLabel="A carregar os destinatários…"
    >
      {(data) => (
        <>
          <Compose count={data.subscribed.length} />
          <Recipients data={data} onChanged={setRecipients} />
        </>
      )}
    </OwnerPage>
  );
}
