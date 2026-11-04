import { useEffect, useState } from 'react'
import { api, fmtDate, fmtTime } from '../api'
import { useAuth } from '../App.jsx'
import { Empty, Field, Modal, Notice, useAction, useLoad } from '../ui.jsx'

const TYPES = [['one_time', 'One-time'], ['daily', 'Daily'], ['weekly', 'Weekly'], ['monthly', 'Monthly']]
const DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']
const blank = () => ({ title: '', description: '', recurrence: 'one_time', startDate: new Date().toISOString().slice(0, 10), endDate: '', startTime: '', endTime: '', dayOfWeek: '1', dayOfMonth: '1', classroomId: '' })
const time = (v) => v ? `${v}:00` : null
const fromItem = (e) => ({ id: e.id, title: e.title, description: e.description || '', recurrence: e.recurrence, startDate: e.startDate, endDate: e.endDate || '', startTime: e.startTime?.slice(0, 5) || '', endTime: e.endTime?.slice(0, 5) || '', dayOfWeek: String(e.dayOfWeek ?? 1), dayOfMonth: String(e.dayOfMonth ?? 1), classroomId: e.classroomId || '' })

export default function Events() {
  const { user, markNotificationsRead } = useAuth()
  const canManage = user.role !== 'parent'
  const [items, reload, loadErr] = useLoad(() => api('/events'))
  const [rooms] = useLoad(() => canManage ? api('/classrooms') : Promise.resolve([]))
  const [editing, setEditing] = useState(null)
  const [run, err] = useAction()
  useEffect(() => { markNotificationsRead('event') }, [markNotificationsRead])
  const save = async (event) => {
    event.preventDefault()
    const body = { ...editing, startTime: time(editing.startTime), endTime: time(editing.endTime), endDate: editing.endDate || null, dayOfWeek: editing.recurrence === 'weekly' ? Number(editing.dayOfWeek) : null, dayOfMonth: editing.recurrence === 'monthly' ? Number(editing.dayOfMonth) : null, classroomId: editing.classroomId || null }
    if (await run(() => api(editing.id ? `/events/${editing.id}` : '/events', { method: editing.id ? 'PUT' : 'POST', body }))) { setEditing(null); reload() }
  }
  const remove = async (id) => { if (window.confirm('Delete this event?') && await run(() => api(`/events/${id}`, { method: 'DELETE' }))) reload() }
  return (
    <>
      <div className="page-heading"><div><h1>Events</h1><p className="muted">Keep families and staff aligned on important daycare events.</p></div>{canManage && <button className="btn" onClick={() => setEditing(blank())}>Add event</button>}</div>
      <Notice>{loadErr || err}</Notice>
      {!items ? null : items.length === 0 ? <Empty>No events yet.</Empty> : <div className="schedule-list">{items.map((item) => <article className="panel schedule-card" key={item.id}>
        <div className="schedule-time"><strong>{item.startTime ? fmtTime(item.startTime) : 'All day'}</strong>{item.endTime && <span>to {fmtTime(item.endTime)}</span>}</div>
        <div className="schedule-content"><div className="schedule-title"><h2>{item.title}</h2><span className="tag">{item.recurrence.replace('_', ' ')}</span></div><p className="muted">{item.classroom || 'Whole daycare'} · {fmtDate(item.startDate)}</p>{item.description && <p className="pre">{item.description}</p>}</div>
        {canManage && <div className="actions schedule-actions"><button className="btn ghost" onClick={() => setEditing(fromItem(item))}>Edit</button><button className="btn ghost danger" onClick={() => remove(item.id)}>Delete</button></div>}
      </article>)}</div>}
      {editing && <Modal title={editing.id ? 'Edit event' : 'Add event'} onClose={() => setEditing(null)}><form className="stack" onSubmit={save}>
        <Field label="Event title"><input required value={editing.title} onChange={(e) => setEditing({ ...editing, title: e.target.value })} placeholder="Family picnic" /></Field>
        <Field label="Description"><textarea rows={3} value={editing.description} onChange={(e) => setEditing({ ...editing, description: e.target.value })} /></Field>
        <div className="grid"><Field label="Repeats"><select value={editing.recurrence} onChange={(e) => setEditing({ ...editing, recurrence: e.target.value })}>{TYPES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select></Field><Field label="Classroom"><select value={editing.classroomId} onChange={(e) => setEditing({ ...editing, classroomId: e.target.value })}><option value="">Whole daycare</option>{rooms?.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}</select></Field><Field label="Date"><input type="date" required value={editing.startDate} onChange={(e) => setEditing({ ...editing, startDate: e.target.value })} /></Field><Field label="End date (optional)"><input type="date" value={editing.endDate} onChange={(e) => setEditing({ ...editing, endDate: e.target.value })} /></Field><Field label="Start time (optional)"><input type="time" value={editing.startTime} onChange={(e) => setEditing({ ...editing, startTime: e.target.value })} /></Field><Field label="End time (optional)"><input type="time" value={editing.endTime} onChange={(e) => setEditing({ ...editing, endTime: e.target.value })} /></Field>{editing.recurrence === 'weekly' && <Field label="Day of week"><select value={editing.dayOfWeek} onChange={(e) => setEditing({ ...editing, dayOfWeek: e.target.value })}>{DAYS.map((d, i) => <option key={d} value={i}>{d}</option>)}</select></Field>}{editing.recurrence === 'monthly' && <Field label="Day of month"><input type="number" min="1" max="31" value={editing.dayOfMonth} onChange={(e) => setEditing({ ...editing, dayOfMonth: e.target.value })} /></Field>}</div>
        <div className="actions"><button className="btn">Save event</button><button type="button" className="btn ghost" onClick={() => setEditing(null)}>Cancel</button></div>
      </form></Modal>}
    </>
  )
}
