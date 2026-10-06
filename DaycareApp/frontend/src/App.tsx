import { FormEvent, useCallback, useEffect, useState } from 'react';
import { api, session, Announcement, Child, LogEntry, Parent, Report, TodayRow, User } from './api';

const fmtTime = (iso?: string) => (iso ? new Date(iso + (iso.endsWith('Z') ? '' : 'Z')).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '');
const fmtDate = (iso: string) => new Date(iso + (iso.endsWith('Z') ? '' : 'Z')).toLocaleDateString([], { month: 'short', day: 'numeric' });
const LOG_TYPES: LogEntry['type'][] = ['Meal', 'Nap', 'Activity', 'Incident'];

function useAsync<T>(load: () => Promise<T>, deps: unknown[]) {
  const [data, setData] = useState<T>();
  const [error, setError] = useState('');
  const reload = useCallback(() => { load().then(setData).catch(e => setError(e.message)); }, deps); // eslint-disable-line
  useEffect(reload, [reload]);
  return { data, error, reload };
}

function Login({ onDone }: { onDone: (u: User) => void }) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  async function submit(e: FormEvent) {
    e.preventDefault(); setError('');
    try {
      const r = await api<{ token: string; user: User }>('/auth/login', { email, password });
      session.set(r); onDone(r.user);
    } catch (err) { setError((err as Error).message); }
  }
  return (
    <form className="panel login stack" onSubmit={submit}>
      <h1>DaycarApp</h1>
      <label>Email<input type="email" required value={email} onChange={e => setEmail(e.target.value)} style={{ width: '100%' }} /></label>
      <label>Password<input type="password" required value={password} onChange={e => setPassword(e.target.value)} style={{ width: '100%' }} /></label>
      {error && <div className="error" role="alert">{error}</div>}
      <button className="btn">Sign in</button>
    </form>
  );
}

function Announcements({ canPost }: { canPost: boolean }) {
  const { data, reload } = useAsync(() => api<Announcement[]>('/announcements'), []);
  const [title, setTitle] = useState('');
  const [body, setBody] = useState('');
  const [error, setError] = useState('');
  async function post(e: FormEvent) {
    e.preventDefault(); setError('');
    try { await api('/announcements', { title, body }); setTitle(''); setBody(''); reload(); }
    catch (err) { setError((err as Error).message); }
  }
  return (
    <>
      {canPost && (
        <form className="panel stack" onSubmit={post}>
          <h3>Post an announcement</h3>
          <input placeholder="Title" value={title} onChange={e => setTitle(e.target.value)} />
          <textarea placeholder="Message for parents" value={body} onChange={e => setBody(e.target.value)} />
          {error && <div className="error" role="alert">{error}</div>}
          <div><button className="btn">Post announcement</button></div>
        </form>
      )}
      <div className="panel">
        <h3>Announcements</h3>
        {data?.length === 0 && <div className="empty">No announcements yet.</div>}
        {data?.map(a => (
          <div className="row" key={a.id}>
            <div><strong>{a.title}</strong><div>{a.body}</div></div>
            <span className="meta">{fmtDate(a.createdAt)}</span>
          </div>
        ))}
      </div>
    </>
  );
}

function TodayView() {
  const { data, error, reload } = useAsync(() => api<TodayRow[]>('/attendance/today'), []);
  const [logFor, setLogFor] = useState<string | null>(null);
  const [type, setType] = useState<LogEntry['type']>('Meal');
  const [notes, setNotes] = useState('');
  const [msg, setMsg] = useState('');

  async function act(path: string, body: unknown) {
    setMsg('');
    try { await api(path, body); reload(); } catch (e) { setMsg((e as Error).message); }
  }
  async function saveLog(e: FormEvent) {
    e.preventDefault();
    await act('/logs', { childId: logFor, type, notes });
    if (notes.trim()) { setNotes(''); setLogFor(null); }
  }
  return (
    <div className="panel">
      <h3>Today</h3>
      {(error || msg) && <div className="error" role="alert">{error || msg}</div>}
      {data?.length === 0 && <div className="empty">No children yet. Add a parent and child in the Children tab.</div>}
      {data?.map(r => (
        <div key={r.id}>
          <div className="row">
            <div>
              <strong>{r.name}</strong>
              {r.allergies && <div className="allergy">Allergies: {r.allergies}</div>}
              <div className="meta">{r.checkIn ? `In ${fmtTime(r.checkIn)}` : 'Not arrived'}{r.checkOut ? ` · Out ${fmtTime(r.checkOut)}` : ''}</div>
            </div>
            <div className="inline">
              {!r.checkIn && <button className="btn" onClick={() => act('/attendance/check-in', { childId: r.id })}>Check in</button>}
              {r.checkIn && !r.checkOut && <>
                <button className="btn ghost" onClick={() => setLogFor(logFor === r.id ? null : r.id)}>Add log</button>
                <button className="btn" onClick={() => act('/attendance/check-out', { childId: r.id })}>Check out</button>
              </>}
              {r.checkOut && <span className="pill out">Went home</span>}
            </div>
          </div>
          {logFor === r.id && (
            <form className="inline" onSubmit={saveLog} style={{ paddingBottom: '.7rem' }}>
              <select value={type} onChange={e => setType(e.target.value as LogEntry['type'])}>{LOG_TYPES.map(t => <option key={t}>{t}</option>)}</select>
              <input placeholder="What happened?" value={notes} onChange={e => setNotes(e.target.value)} style={{ flex: 1 }} />
              <button className="btn">Save log</button>
            </form>
          )}
        </div>
      ))}
    </div>
  );
}

function ChildrenView() {
  const kids = useAsync(() => api<Child[]>('/children'), []);
  const parents = useAsync(() => api<Parent[]>('/parents'), []);
  const [p, setP] = useState({ name: '', email: '', phone: '', password: '' });
  const [c, setC] = useState({ name: '', dateOfBirth: '', allergies: '', parentId: '' });
  const [error, setError] = useState('');

  async function addParent(e: FormEvent) {
    e.preventDefault(); setError('');
    try { await api('/parents', p); setP({ name: '', email: '', phone: '', password: '' }); parents.reload(); }
    catch (err) { setError((err as Error).message); }
  }
  async function addChild(e: FormEvent) {
    e.preventDefault(); setError('');
    try { await api('/children', { ...c, allergies: c.allergies || null }); setC({ name: '', dateOfBirth: '', allergies: '', parentId: '' }); kids.reload(); }
    catch (err) { setError((err as Error).message); }
  }
  const parentName = (id: string) => parents.data?.find(x => x.id === id)?.name ?? '';
  return (
    <>
      {error && <div className="error" role="alert">{error}</div>}
      <form className="panel stack" onSubmit={addParent}>
        <h3>Add a parent</h3>
        <div className="inline">
          <input placeholder="Full name" required value={p.name} onChange={e => setP({ ...p, name: e.target.value })} />
          <input type="email" placeholder="Email" required value={p.email} onChange={e => setP({ ...p, email: e.target.value })} />
          <input placeholder="Phone" value={p.phone} onChange={e => setP({ ...p, phone: e.target.value })} />
          <input type="password" placeholder="Temporary password (8+ characters)" required minLength={8} value={p.password} onChange={e => setP({ ...p, password: e.target.value })} />
          <button className="btn">Add parent</button>
        </div>
      </form>
      <form className="panel stack" onSubmit={addChild}>
        <h3>Add a child</h3>
        <div className="inline">
          <input placeholder="Child's name" required value={c.name} onChange={e => setC({ ...c, name: e.target.value })} />
          <input type="date" required aria-label="Date of birth" value={c.dateOfBirth} onChange={e => setC({ ...c, dateOfBirth: e.target.value })} />
          <input placeholder="Allergies (optional)" value={c.allergies} onChange={e => setC({ ...c, allergies: e.target.value })} />
          <select required aria-label="Parent" value={c.parentId} onChange={e => setC({ ...c, parentId: e.target.value })}>
            <option value="">Choose parent</option>
            {parents.data?.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}
          </select>
          <button className="btn">Add child</button>
        </div>
      </form>
      <div className="panel">
        <h3>Children</h3>
        {kids.data?.length === 0 && <div className="empty">No children yet.</div>}
        {kids.data?.map(k => (
          <div className="row" key={k.id}>
            <div><strong>{k.name}</strong>{k.allergies && <div className="allergy">Allergies: {k.allergies}</div>}</div>
            <span className="meta">Parent: {parentName(k.parentId)}</span>
          </div>
        ))}
      </div>
    </>
  );
}

function ParentView() {
  const kids = useAsync(() => api<Child[]>('/children'), []);
  const [childId, setChildId] = useState('');
  const [date, setDate] = useState('');
  const id = childId || kids.data?.[0]?.id;
  const report = useAsync(() => (id ? api<Report>(`/children/${id}/report${date ? `?date=${date}` : ''}`) : Promise.resolve(undefined)), [id, date]);
  const r = report.data;
  return (
    <>
      <div className="panel stack">
        <div className="inline">
          {kids.data && kids.data.length > 1 && (
            <select aria-label="Child" value={id} onChange={e => setChildId(e.target.value)}>{kids.data.map(k => <option key={k.id} value={k.id}>{k.name}</option>)}</select>
          )}
          <input type="date" aria-label="Report date" value={date} onChange={e => setDate(e.target.value)} />
        </div>
        {kids.data?.length === 0 && <div className="empty">No children are linked to your account yet. Ask the daycare to add them.</div>}
        {r && (
          <>
            <h2>{r.child.name}'s day</h2>
            <div className="meta">{r.checkIn ? `Arrived ${fmtTime(r.checkIn)}` : 'No check-in recorded'}{r.checkOut ? ` · Picked up ${fmtTime(r.checkOut)}` : ''}</div>
            {r.logs.length === 0 && <div className="empty">No activity logged for this day.</div>}
            {r.logs.map(l => (
              <div className="row" key={l.id}>
                <div><span className={`pill ${l.type === 'Incident' ? 'incident' : ''}`}>{l.type}</span> {l.notes}</div>
                <span className="meta">{fmtTime(l.at)}</span>
              </div>
            ))}
          </>
        )}
      </div>
      <Announcements canPost={false} />
    </>
  );
}

export default function App() {
  const [user, setUser] = useState<User | null>(session.get()?.user ?? null);
  const [tab, setTab] = useState<'today' | 'children' | 'news'>('today');
  if (!user) return <Login onDone={setUser} />;
  const isStaff = user.role !== 'Parent';
  return (
    <div className="shell">
      <header className="top">
        <h1>DaycarApp</h1>
        <div className="who">{user.name}<button className="btn ghost" onClick={() => { session.clear(); setUser(null); }}>Sign out</button></div>
      </header>
      {isStaff ? (
        <>
          <nav className="tabs" role="tablist">
            {([['today', 'Today'], ['children', 'Children'], ['news', 'Announcements']] as const).map(([k, label]) => (
              <button key={k} role="tab" aria-selected={tab === k} onClick={() => setTab(k)}>{label}</button>
            ))}
          </nav>
          {tab === 'today' && <TodayView />}
          {tab === 'children' && <ChildrenView />}
          {tab === 'news' && <Announcements canPost />}
        </>
      ) : <ParentView />}
    </div>
  );
}
