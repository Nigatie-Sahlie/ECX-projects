import { useEffect, useState } from 'react'
import { api, fmtDate, fmtTime } from '../api'
import { useAuth } from '../App.jsx'
import { Empty, Field, Modal, Notice, useAction, useLoad } from '../ui.jsx'

const RECURRENCES = [
  ['daily', 'Daily'],
  ['weekly', 'Weekly'],
  ['monthly', 'Monthly'],
]
const DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']
const today = () => new Date().toISOString().slice(0, 10)
const blank = () => ({
  title: '', description: '', recurrence: 'daily', startDate: today(), endDate: '',
  startTime: '09:00', endTime: '10:00', dayOfWeek: '1', dayOfMonth: '1', classroomId: '',
})

const apiTime = (value) => value ? `${value}:00` : null

function formFrom(item) {
  return {
    title: item.title, description: item.description || '', recurrence: item.recurrence,
    startDate: item.startDate, endDate: item.endDate || '',
    startTime: item.startTime.slice(0, 5), endTime: item.endTime?.slice(0, 5) || '',
    dayOfWeek: String(item.dayOfWeek ?? 1), dayOfMonth: String(item.dayOfMonth ?? 1),
    classroomId: item.classroomId || '',
  }
}

export default function Schedule() {
  const { user, markNotificationsRead } = useAuth()
  const canManage = user.role !== 'parent'
  const [items, reload, loadErr] = useLoad(() => api('/schedules'))
  const [rooms] = useLoad(() => (canManage ? api('/classrooms') : Promise.resolve([])))
  const [editing, setEditing] = useState(null)
  const [showForm, setShowForm] = useState(false)
  const [filter, setFilter] = useState('all')
  const [run, err] = useAction()
  useEffect(() => { markNotificationsRead('schedule') }, [markNotificationsRead])
  useEffect(() => {
    const refresh = (event) => {
      if (event.detail?.type === 'schedule') reload()
    }
    window.addEventListener('app-notification', refresh)
    return () => window.removeEventListener('app-notification', refresh)
  }, [reload])

  const save = async (event) => {
    event.preventDefault()
    const body = {
      ...editing,
      startTime: apiTime(editing.startTime),
      endTime: apiTime(editing.endTime),
      endDate: editing.endDate || null,
      dayOfWeek: editing.recurrence === 'weekly' ? Number(editing.dayOfWeek) : null,
      dayOfMonth: editing.recurrence === 'monthly' ? Number(editing.dayOfMonth) : null,
      classroomId: editing.classroomId || null,
    }
    const path = editing.id ? `/schedules/${editing.id}` : '/schedules'
    if (await run(() => api(path, { method: editing.id ? 'PUT' : 'POST', body }))) {
      setShowForm(false)
      setEditing(null)
      reload()
    }
  }

  const remove = async (id) => {
    if (window.confirm('Delete this schedule?') &&
      await run(() => api(`/schedules/${id}`, { method: 'DELETE' }))) reload()
  }

  const visible = items?.filter((item) => filter === 'all' || item.recurrence === filter) || []

  return (
    <>
      <div className="page-heading">
        <div><h1>Schedule</h1><p className="muted">Plan daily routines and recurring daycare activities.</p></div>
        {canManage && <button className="btn" onClick={() => { setEditing(blank()); setShowForm(true) }}>Add schedule</button>}
      </div>
      <Notice>{loadErr || err}</Notice>
      <div className="schedule-filters" role="tablist" aria-label="Schedule frequency">
        <button className={filter === 'all' ? 'active' : ''} onClick={() => setFilter('all')}>All schedules</button>
        {RECURRENCES.map(([value, label]) => <button className={filter === value ? 'active' : ''} key={value} onClick={() => setFilter(value)}>{label}</button>)}
      </div>
      {!items ? null : visible.length === 0 ? <Empty>No schedules for this view yet.</Empty> : (
        <div className="schedule-list">
          {visible.map((item) => (
            <article className="panel schedule-card" key={item.id}>
              <div className="schedule-time">
                <strong>{fmtTime(item.startTime)}</strong>
                {item.endTime && <span>to {fmtTime(item.endTime)}</span>}
              </div>
              <div className="schedule-content">
                <div className="schedule-title"><h2>{item.title}</h2><span className="tag">{item.recurrence}</span></div>
                <p className="muted">{item.classroom || 'Whole daycare'} · Starts {fmtDate(item.startDate)}</p>
                {item.description && <p className="pre">{item.description}</p>}
                <small className="muted">
                  {item.recurrence === 'weekly' ? `Every ${DAYS[item.dayOfWeek]}` :
                    item.recurrence === 'monthly' ? `Every month on day ${item.dayOfMonth}` : 'Every day'}
                  {item.endDate ? ` until ${fmtDate(item.endDate)}` : ''}
                </small>
              </div>
              {canManage && <div className="actions schedule-actions">
                <button className="btn ghost" onClick={() => { setEditing({ ...formFrom(item), id: item.id }); setShowForm(true) }}>Edit</button>
                <button className="btn ghost danger" onClick={() => remove(item.id)}>Delete</button>
              </div>}
            </article>
          ))}
        </div>
      )}
      {showForm && <Modal title={editing.id ? 'Edit schedule' : 'Add schedule'} onClose={() => { setShowForm(false); setEditing(null) }}>
        <form className="stack" onSubmit={save}>
          <Field label="Activity title"><input required value={editing.title} onChange={(e) => setEditing({ ...editing, title: e.target.value })} placeholder="Morning circle" /></Field>
          <Field label="Description"><textarea rows={3} value={editing.description} onChange={(e) => setEditing({ ...editing, description: e.target.value })} placeholder="Optional details for staff and families" /></Field>
          <div className="grid">
            <Field label="Repeats"><select value={editing.recurrence} onChange={(e) => setEditing({ ...editing, recurrence: e.target.value })}>{RECURRENCES.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></Field>
            <Field label="Classroom"><select value={editing.classroomId} onChange={(e) => setEditing({ ...editing, classroomId: e.target.value })}><option value="">Whole daycare</option>{rooms?.map((room) => <option key={room.id} value={room.id}>{room.name}</option>)}</select></Field>
            <Field label="Starts on"><input type="date" required value={editing.startDate} onChange={(e) => setEditing({ ...editing, startDate: e.target.value })} /></Field>
            <Field label="Ends on (optional)"><input type="date" value={editing.endDate} onChange={(e) => setEditing({ ...editing, endDate: e.target.value })} /></Field>
            <Field label="Start time"><input type="time" required value={editing.startTime} onChange={(e) => setEditing({ ...editing, startTime: e.target.value })} /></Field>
            <Field label="End time (optional)"><input type="time" value={editing.endTime} onChange={(e) => setEditing({ ...editing, endTime: e.target.value })} /></Field>
            {editing.recurrence === 'weekly' && <Field label="Day of week"><select value={editing.dayOfWeek} onChange={(e) => setEditing({ ...editing, dayOfWeek: e.target.value })}>{DAYS.map((day, index) => <option key={day} value={index}>{day}</option>)}</select></Field>}
            {editing.recurrence === 'monthly' && <Field label="Day of month"><input type="number" min="1" max="31" required value={editing.dayOfMonth} onChange={(e) => setEditing({ ...editing, dayOfMonth: e.target.value })} /></Field>}
          </div>
          <div className="actions"><button className="btn">Save schedule</button><button type="button" className="btn ghost" onClick={() => { setShowForm(false); setEditing(null) }}>Cancel</button></div>
        </form>
      </Modal>}
    </>
  )
}
