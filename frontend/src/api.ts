export type Role = 'student' | 'lecturer';

export interface Me {
  accountId: string;
  displayName: string;
  role: Role;
}

export interface CourseResult {
  courseId: string;
  courseTitle: string;
  semester: string;
  score: number | null;
  grade: string | null;
  updatedAt: string | null;
}

export interface StudentRecord {
  studentId: string;
  name: string;
  email: string;
  programme: string;
  yearGroup: number;
  courses: CourseResult[];
}

export interface CourseSummary {
  courseId: string;
  title: string;
  semester: string;
  studentCount: number;
}

export interface RosterEntry {
  studentId: string;
  name: string;
  programme: string;
  score: number | null;
  grade: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
}

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
  }
}

async function request<T>(method: 'GET' | 'POST', url: string, body?: unknown): Promise<T> {
  const response = await fetch(url, {
    method,
    credentials: 'same-origin',
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (!response.ok) {
    let message = `Request failed (${response.status}).`;
    try {
      const data = await response.json();
      message = data.error ?? data.title ?? message;
    } catch {
      // Body was not JSON.
    }
    throw new ApiError(response.status, message);
  }

  return response.status === 204 ? (undefined as T) : ((await response.json()) as T);
}

const segment = encodeURIComponent;

export const api = {
  me: () => request<Me>('GET', '/api/auth/me'),
  logout: () => request<void>('POST', '/api/auth/logout'),
  signInOptions: () => request<unknown>('POST', '/api/auth/options'),
  signInVerify: (credential: unknown) => request<Me>('POST', '/api/auth/verify', credential),
  enrollmentBegin: (accountId: string, code: string) =>
    request<unknown>('POST', '/api/enrollment/begin', { accountId, code }),
  enrollmentComplete: (credential: unknown) =>
    request<{ accountId: string; message: string }>('POST', '/api/enrollment/complete', credential),
  student: (studentId: string) => request<StudentRecord>('GET', `/api/students/${segment(studentId)}`),
  myCourses: () => request<CourseSummary[]>('GET', '/api/courses/mine'),
  roster: (courseId: string) => request<RosterEntry[]>('GET', `/api/courses/${segment(courseId)}/roster`),
  updateGrade: (courseId: string, studentId: string, score: number) =>
    request<RosterEntry>('POST', `/api/courses/${segment(courseId)}/grades/${segment(studentId)}`, { score }),
};
