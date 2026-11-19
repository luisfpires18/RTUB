// Owner users role list and message type.

export const ROLES = [
  { value: 'Member', hint: 'Acesso básico aos recursos da aplicação.' },
  { value: 'Mod', hint: 'Acesso moderado, incluindo inventário, loja e logística.' },
  { value: 'Admin', hint: 'Acesso às funcionalidades administrativas.' },
  { value: 'Owner', hint: 'Acesso total, incluindo estas ferramentas do Owner.' },
];

export type Message = { text: string; error?: boolean };
