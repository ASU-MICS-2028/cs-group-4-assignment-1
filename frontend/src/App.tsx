import { useCallback, useEffect, useState } from 'react';
import { api, type Me } from './api';
import Header from './components/Header';
import Landing from './pages/Landing';
import StudentDashboard from './pages/StudentDashboard';
import LecturerDashboard from './pages/LecturerDashboard';

export default function App() {
  const [me, setMe] = useState<Me | null | undefined>(undefined);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    api
      .me()
      .then(setMe)
      .catch(() => setMe(null));
  }, []);

  const signOut = useCallback(async () => {
    try {
      await api.logout();
    } finally {
      setMe(null);
      setNotice('You have signed out.');
    }
  }, []);

  const sessionEnded = useCallback(() => {
    setMe(null);
    setNotice('Your session has ended. Please sign in again.');
  }, []);

  const signedIn = (account: Me) => {
    setNotice(null);
    setMe(account);
  };

  return (
    <div className="page">
      <Header me={me ?? null} onSignOut={signOut} />
      <main className="content">
        {me === undefined && <p className="muted">Loading…</p>}
        {me === null && <Landing notice={notice} onSignedIn={signedIn} />}
        {me?.role === 'student' && <StudentDashboard me={me} onSessionEnded={sessionEnded} />}
        {me?.role === 'lecturer' && <LecturerDashboard me={me} onSessionEnded={sessionEnded} />}
      </main>
      <footer className="footer">ICS570 Cybersecurity Essentials · Lab 2 · Synthetic data for coursework only</footer>
    </div>
  );
}
