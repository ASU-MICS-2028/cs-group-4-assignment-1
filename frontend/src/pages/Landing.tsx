import { useState, type FormEvent } from 'react';
import { api, type Me } from '../api';
import Message from '../components/Message';
import { createPasskey, describeError, getPasskey, passkeysSupported } from '../webauthn';

interface Props {
  notice: string | null;
  onSignedIn: (me: Me) => void;
}

export default function Landing({ notice, onSignedIn }: Props) {
  const [busy, setBusy] = useState<'signin' | 'enroll' | null>(null);
  const [signInError, setSignInError] = useState<string | null>(null);
  const [enrollError, setEnrollError] = useState<string | null>(null);
  const [enrolled, setEnrolled] = useState<string | null>(null);
  const [accountId, setAccountId] = useState('');
  const [code, setCode] = useState('');

  const signIn = async () => {
    setBusy('signin');
    setSignInError(null);
    try {
      const options = await api.signInOptions();
      const credential = await getPasskey(options);
      onSignedIn(await api.signInVerify(credential));
    } catch (error) {
      setSignInError(describeError(error));
    } finally {
      setBusy(null);
    }
  };

  const enroll = async (event: FormEvent) => {
    event.preventDefault();
    setBusy('enroll');
    setEnrollError(null);
    setEnrolled(null);
    try {
      const options = await api.enrollmentBegin(accountId.trim(), code.trim());
      const credential = await createPasskey(options);
      const result = await api.enrollmentComplete(credential);
      setEnrolled(result.accountId);
      setCode('');
    } catch (error) {
      setEnrollError(describeError(error));
    } finally {
      setBusy(null);
    }
  };

  if (!passkeysSupported()) {
    return (
      <Message kind="error">
        This browser does not support passkeys. Use a current version of Chrome, Edge, Firefox or Safari.
      </Message>
    );
  }

  return (
    <>
      {notice && <Message kind="info">{notice}</Message>}
      <div className="landing">
        <section className="card card-accent">
          <h1>Sign in</h1>
          <p className="muted">
            Use the passkey on this device. Your browser will ask for your fingerprint, face or device PIN. No password
            is sent to the portal.
          </p>
          <button type="button" className="button button-primary button-wide" onClick={signIn} disabled={busy !== null}>
            {busy === 'signin' ? 'Waiting for passkey…' : 'Sign in with a passkey'}
          </button>
          {signInError && <Message kind="error">{signInError}</Message>}
        </section>

        <section className="card">
          <h2>Activate your account</h2>
          <p className="muted">
            First time here? Enter your account ID and the one-time enrollment code issued by the IT help desk, then
            create a passkey on this device.
          </p>
          <form onSubmit={enroll} className="form">
            <label>
              Account ID
              <input
                value={accountId}
                onChange={(e) => setAccountId(e.target.value)}
                placeholder="e.g. S001"
                autoComplete="username"
                required
                maxLength={10}
              />
            </label>
            <label>
              Enrollment code
              <input
                value={code}
                onChange={(e) => setCode(e.target.value)}
                placeholder="XXXX-XXXX-XXXX"
                autoComplete="one-time-code"
                required
                maxLength={32}
              />
            </label>
            <button type="submit" className="button button-secondary button-wide" disabled={busy !== null}>
              {busy === 'enroll' ? 'Creating passkey…' : 'Create passkey'}
            </button>
          </form>
          {enrollError && <Message kind="error">{enrollError}</Message>}
          {enrolled && <Message kind="success">Passkey registered for {enrolled}. You can now sign in.</Message>}
        </section>
      </div>
    </>
  );
}
