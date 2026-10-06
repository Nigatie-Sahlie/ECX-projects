export type Role = 'Admin' | 'Staff' | 'Parent';
export interface User { id: string; name: string; role: Role }
export interface Child { id: string; name: string; dateOfBirth: string; allergies?: string; parentId: string }
export interface Parent { id: string; name: string; email: string; phone?: string }
export interface TodayRow { id: string; name: string; allergies?: string; checkIn?: string; checkOut?: string }
export interface LogEntry { id: string; type: 'Meal' | 'Nap' | 'Activity' | 'Incident'; notes: string; at: string }
export interface Report { child: { id: string; name: string }; date: string; checkIn?: string; checkOut?: string; logs: LogEntry[] }
export interface Announcement { id: string; title: string; body: string; createdAt: string }

const KEY = 'daycare.session';
export const session = {
  get(): { token: string; user: User } | null {
    try { return JSON.parse(localStorage.getItem(KEY) ?? 'null'); } catch { return null; }
  },
  set(v: { token: string; user: User }) { localStorage.setItem(KEY, JSON.stringify(v)); },
  clear() { localStorage.removeItem(KEY); }
};

export async function api<T = unknown>(path: string, body?: unknown): Promise<T> {
  const s = session.get();
  const res = await fetch('/api' + path, {
    method: body === undefined ? 'GET' : 'POST',
    headers: { 'Content-Type': 'application/json', ...(s ? { Authorization: `Bearer ${s.token}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body)
  });
  if (res.status === 401 && s) { session.clear(); location.reload(); }
  if (!res.ok) {
    const data = await res.json().catch(() => null);
    throw new Error(data?.error ?? (res.status === 401 ? 'Wrong email or password.' : 'Something went wrong. Try again.'));
  }
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}
