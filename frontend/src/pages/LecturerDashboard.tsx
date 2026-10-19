import { useCallback, useEffect, useState } from 'react';
import { ApiError, api, type CourseSummary, type Me, type RosterEntry, type StudentRecord } from '../api';
import Message from '../components/Message';
import { formatDate, formatScore } from '../format';

interface Props {
  me: Me;
  onSessionEnded: () => void;
}

export default function LecturerDashboard({ me, onSessionEnded }: Props) {
  const [courses, setCourses] = useState<CourseSummary[] | null>(null);
  const [courseId, setCourseId] = useState<string | null>(null);
  const [roster, setRoster] = useState<RosterEntry[]>([]);
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const [viewing, setViewing] = useState<StudentRecord | null>(null);
  const [message, setMessage] = useState<{ kind: 'error' | 'success'; text: string } | null>(null);

  const fail = useCallback(
    (e: unknown) => {
      if (e instanceof ApiError && e.status === 401) onSessionEnded();
      else setMessage({ kind: 'error', text: (e as Error).message });
    },
    [onSessionEnded],
  );

  useEffect(() => {
    api
      .myCourses()
      .then((list) => {
        setCourses(list);
        setCourseId(list[0]?.courseId ?? null);
      })
      .catch(fail);
  }, [fail]);

  const loadRoster = useCallback(
    (id: string) => {
      api
        .roster(id)
        .then((entries) => {
          setRoster(entries);
          setDrafts(Object.fromEntries(entries.map((e) => [e.studentId, e.score?.toString() ?? ''])));
        })
        .catch(fail);
    },
    [fail],
  );

  useEffect(() => {
    if (courseId) loadRoster(courseId);
  }, [courseId, loadRoster]);

  const selectCourse = (id: string) => {
    setCourseId(id);
    setViewing(null);
  };

  const save = async (studentId: string) => {
    if (!courseId) return;
    const score = Number(drafts[studentId]);
    if (!Number.isInteger(score) || score < 0 || score > 100) {
      setMessage({ kind: 'error', text: 'Enter a whole-number score from 0 to 100.' });
      return;
    }
    try {
      const updated = await api.updateGrade(courseId, studentId, score);
      setMessage({ kind: 'success', text: `Saved ${studentId}: ${updated.score} (${updated.grade}).` });
      loadRoster(courseId);
    } catch (e) {
      fail(e);
    }
  };

  const view = async (studentId: string) => {
    try {
      setViewing(await api.student(studentId));
    } catch (e) {
      fail(e);
    }
  };

  if (courses === null) return <p className="muted">Loading your courses…</p>;

  const course = courses.find((c) => c.courseId === courseId);

  return (
    <div className="stack">
      <section className="card card-accent">
        <h1>Welcome, {me.displayName}</h1>
        <p className="muted">You can view and grade students in the courses currently assigned to you.</p>
        {courses.length === 0 ? (
          <Message kind="info">You have no current course assignments.</Message>
        ) : (
          <div className="course-tabs" role="tablist">
            {courses.map((c) => (
              <button
                key={c.courseId}
                type="button"
                role="tab"
                aria-selected={c.courseId === courseId}
                className={`course-tab${c.courseId === courseId ? ' active' : ''}`}
                onClick={() => selectCourse(c.courseId)}
              >
                <span className="mono">{c.courseId}</span> {c.title}
                <span className="course-count">{c.studentCount} students</span>
              </button>
            ))}
          </div>
        )}
      </section>

      {message && <Message kind={message.kind}>{message.text}</Message>}

      {course && (
        <section className="card">
          <h2>
            {course.courseId} roster · {course.semester}
          </h2>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Student</th>
                  <th>Name</th>
                  <th>Programme</th>
                  <th className="num">Score</th>
                  <th>Grade</th>
                  <th>Last updated</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {roster.map((r) => (
                  <tr key={r.studentId}>
                    <td className="mono">{r.studentId}</td>
                    <td>{r.name}</td>
                    <td>{r.programme}</td>
                    <td className="num">
                      <input
                        className="score-input"
                        inputMode="numeric"
                        aria-label={`Score for ${r.studentId}`}
                        value={drafts[r.studentId] ?? ''}
                        onChange={(e) => setDrafts((d) => ({ ...d, [r.studentId]: e.target.value }))}
                      />
                    </td>
                    <td>
                      <span className="grade">{r.grade ?? '—'}</span>
                    </td>
                    <td>
                      {formatDate(r.updatedAt)}
                      {r.updatedBy ? ` · ${r.updatedBy}` : ''}
                    </td>
                    <td>
                      <div className="actions">
                        <button
                          type="button"
                          className="button button-primary button-small"
                          onClick={() => save(r.studentId)}
                        >
                          Save
                        </button>
                        <button
                          type="button"
                          className="button button-outline button-small"
                          onClick={() => view(r.studentId)}
                        >
                          View record
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {viewing && (
        <section className="card">
          <h2>
            {viewing.name} ({viewing.studentId})
          </h2>
          <p className="muted">
            {viewing.programme}, year group {viewing.yearGroup}. Only results for courses you teach are shown.
          </p>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Course</th>
                  <th>Title</th>
                  <th className="num">Score</th>
                  <th>Grade</th>
                </tr>
              </thead>
              <tbody>
                {viewing.courses.map((c) => (
                  <tr key={c.courseId}>
                    <td className="mono">{c.courseId}</td>
                    <td>{c.courseTitle}</td>
                    <td className="num">{formatScore(c.score)}</td>
                    <td>
                      <span className="grade">{c.grade ?? '—'}</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}
    </div>
  );
}
