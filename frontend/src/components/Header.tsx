import type { Me } from '../api';

interface Props {
  me: Me | null;
  onSignOut: () => void;
}

export default function Header({ me, onSignOut }: Props) {
  return (
    <header className="header">
      <div className="header-inner">
        <div className="brand">
          <img src="/ashesi-logo.png" alt="Ashesi University" className="brand-logo" />
          <div>
            <div className="brand-title">Student Records Portal</div>
            <div className="brand-subtitle">Computer Science and Information Systems</div>
          </div>
        </div>
        {me && (
          <div className="account">
            <div className="account-text">
              <span className="account-name">{me.displayName}</span>
              <span className="account-meta">
                {me.accountId} · <span className={`role role-${me.role}`}>{me.role}</span>
              </span>
            </div>
            <button type="button" className="button button-outline" onClick={onSignOut}>
              Sign out
            </button>
          </div>
        )}
      </div>
    </header>
  );
}
