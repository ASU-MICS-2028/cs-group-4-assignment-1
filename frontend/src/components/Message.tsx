import type { ReactNode } from 'react';

interface Props {
  kind: 'error' | 'success' | 'info';
  children: ReactNode;
}

export default function Message({ kind, children }: Props) {
  return (
    <div className={`message message-${kind}`} role={kind === 'error' ? 'alert' : 'status'}>
      {children}
    </div>
  );
}
