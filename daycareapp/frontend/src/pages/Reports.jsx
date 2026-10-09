import { useState } from 'react'
import { api, fmtDate, fmtTime, today } from '../api'
import { useAuth } from '../App.jsx'
import { Empty, Field, Notice, useAction, useLoad } from '../ui.jsx'

const TYPES = [
  ['meal', 'Meal'], ['nap', 'Nap'], ['diaper', 'Diaper'],
  ['activity', 'Activity'], ['mood', 'Mood'], ['note', 'Note'],
]
const label = (t) => TYPES.find((x) => x[0] === t)?.[1] ?? t

function Entries({ entries, onDelete }) {
  if (!entries?.length) return <Empty>Nothing logged yet.</Empty>
  return (
    <ul className="timeline">
      {entries.map((e) => (
        <li key={e.id}>
          <span className="time">{fmtTime(e.occurredAt)}</span>
          <span className="tag">{label(e.entryType)}</span>
          <span className="grow">{e.details}</span>
          {onDelete && <button className="link" onClick={() => onDelete(e.id)}>Remove</button>}
        </li>
      ))}
    </ul>
  )
}

// ---- teachers and admins ----
function Write() {
  const { user } = useAuth()
  const canWrite = user.role === 'staff'
  const [children] = useLoad(() => api('/children'))
  const [childId, setChildId] = useState('')
  const [date, setDate] = useState(today())
  const [reports, reload, loadErr] = useLoad(
    () => (childId ? api(`/reports?childId=${childId}&date=${date}`) : Promise.resolve([])),
    [childId, date]
  )
  const report = reports?.[0]
  const [entry, setEntry] = useState({ entryType: 'meal', details: '', time: new Date().toTimeString().slice(0, 5) })
  const [note, setNote] = useState(null) // null = use saved note
  const [run, err] = useAction()
  const noteValue = note ?? report?.generalNote ?? ''

  const addEntry = async (e) => {
    e.preventDefault()
    const occurredAt = new Date(`${date}T${entry.time}:00`).toISOString()
    if (await run(() => api('/reports/entries', { method: 'POST', body: { childId, date, entryType: entry.entryType, details: entry.details, occurredAt } }))) {
      setEntry({ ...entry, details: '' })
      reload()
    }
  }
  const removeEntry = (id) => run(() => api(`/reports/entries/${id}`, { method: 'DELETE' })).then(reload)
  const save = async (publish) => {
    if (await run(() => api(`/reports/${report.id}`, { method: 'PUT', body: { generalNote: noteValue, publish } }))) {
      setNote(null)
      reload()
    }
  }

  return (
    <>
      <h1>Daily reports</h1>
      <div className="toolbar">
        <label className="field inline">
          <span>Child</span>
          <select value={childId} onChange={(e) => { setChildId(e.target.value); setNote(null) }}>
            <option value="">Choose a child…</option>
            {children?.filter((c) => c.isActive).map((c) => <option key={c.id} value={c.id}>{c.firstName} {c.lastName}</option>)}
          </select>
        </label>
        <label className="field inline"><span>Day</span><input type="date" value={date} max={today()} onChange={(e) => { setDate(e.target.value || today()); setNote(null) }} /></label>
      </div>
      <Notice>{loadErr || err}</Notice>

      {!childId ? <Empty>Choose a child to write today's report.</Empty> : (
        <>
          <section className="panel">
            <h2>Add to the day</h2>
            {canWrite && <form className="grid" onSubmit={addEntry}>
              <Field label="What happened">
                <select value={entry.entryType} onChange={(e) => setEntry({ ...entry, entryType: e.target.value })}>
                  {TYPES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
                </select>
              </Field>
              <Field label="Time"><input type="time" required value={entry.time} onChange={(e) => setEntry({ ...entry, time: e.target.value })} /></Field>
              <Field label="Details"><input required placeholder="e.g. Ate rice and lentils, finished half" value={entry.details} onChange={(e) => setEntry({ ...entry, details: e.target.value })} /></Field>
              <div className="full"><button className="btn">Add entry</button></div>
            </form>}
          </section>

          <section className="panel">
            <h2>
              {fmtDate(date)} {report && (report.publishedAt ? <span className="tag ok">Shared with parents</span> : <span className="tag warn">Draft</span>)}
            </h2>
            <Entries entries={report?.entries} onDelete={canWrite ? removeEntry : undefined} />
            {report && canWrite && (
              <>
                <Field label="Summary for parents">
                  <textarea rows={3} value={noteValue} onChange={(e) => setNote(e.target.value)} />
                </Field>
                <div className="actions">
                  <button className="btn" onClick={() => save(true)}>{report.publishedAt ? 'Update shared report' : 'Share with parents'}</button>
                  {report.publishedAt && <button className="btn ghost" onClick={() => save(false)}>Unshare</button>}
                  {!report.publishedAt && <button className="btn ghost" onClick={() => save(false)}>Save draft</button>}
                </div>
              </>
            )}
          </section>
        </>
      )}
    </>
  )
}

// ---- parents ----
function Read() {
  const [children] = useLoad(() => api('/children'))
  const [childId, setChildId] = useState('')
  const [reports, , loadErr] = useLoad(() => api(`/reports${childId ? `?childId=${childId}` : ''}`), [childId])

  return (
    <>
      <h1>Daily reports</h1>
      <div className="toolbar">
        <label className="field inline">
          <span>Child</span>
          <select value={childId} onChange={(e) => setChildId(e.target.value)}>
            <option value="">All my children</option>
            {children?.map((c) => <option key={c.id} value={c.id}>{c.firstName}</option>)}
          </select>
        </label>
      </div>
      <Notice>{loadErr}</Notice>
      {!reports ? null : reports.length === 0 ? (
        <Empty>No reports yet. Your child's teacher will share one at the end of the day.</Empty>
      ) : (
        reports.map((r) => (
          <section className="panel" key={r.id}>
            <h2>{r.child} · {fmtDate(r.reportDate)}</h2>
            {r.generalNote && <p className="summary-note">{r.generalNote}</p>}
            <Entries entries={r.entries} />
          </section>
        ))
      )}
    </>
  )
}

export default function Reports() {
  const { user } = useAuth()
  return user.role === 'parent' ? <Read /> : <Write />
}
