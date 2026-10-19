import { useEffect, useState } from 'react';
import { ApiError, api, type Me, type StudentRecord } from '../api';
import Message from '../components/Message';
import { formatDate, formatScore } from '../format';

interface Props {
  me: Me;
  onSessionEnded: () => void;
}

export default function StudentDashboard({ me, onSessionEnded }: Props) {
  const [record, setRecord] = useState<StudentRecord | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api
      .student(me.accountId)
      .then(setRecord)
      .catch((e: unknown) =>
        e instanceof ApiError && e.status === 401 ? onSessionEnded() : setError(String((e as Error).message)),
      );
  }, [me.accountId, onSessionEnded]);

  if (error) return <Message kind="error">{error}</Message>;
  if (!record) return <p className="muted">Loading your record…</p>;

  return (
    <div className="stack">
      <section className="card card-accent">
        <h1>{record.name}</h1>
        <dl className="profile">
          <div>
            <dt>Student ID</dt>
            <dd>{record.studentId}</dd>
          </div>
          <div>
            <dt>Programme</dt>
            <dd>{record.programme}</dd>
          </div>
          <div>
            <dt>Year group</dt>
            <dd>{record.yearGroup}</dd>
          </div>
          <div>
            <dt>Email</dt>
            <dd>{record.email}</dd>
          </div>
        </dl>
      </section>

      <section className="card">
        <h2>Academic record</h2>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Course</th>
                <th>Title</th>
                <th>Semester</th>
                <th className="num">Score</th>
                <th>Grade</th>
                <th>Last updated</th>
              </tr>
            </thead>
            <tbody>
              {record.courses.map((c) => (
                <tr key={c.courseId}>
                  <td className="mono">{c.courseId}</td>
                  <td>{c.courseTitle}</td>
                  <td>{c.semester}</td>
                  <td className="num">{formatScore(c.score)}</td>
                  <td>
                    <span className="grade">{c.grade ?? '—'}</span>
                  </td>
                  <td>{formatDate(c.updatedAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <p className="muted small">
          Grades follow the Ashesi graduate scale (A ≥ 80, B+ 75–79, B 70–74, C+ 65–69, C 60–64, D+ 55–59, D 50–54, E
          &lt; 50).
        </p>
      </section>
    </div>
  );
}
