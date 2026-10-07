import { useState } from 'react'
import { api, fmtDate, fmtTime, today } from '../api'
import { useAuth } from '../App.jsx'
import { Empty, Notice, useAction, useLoad } from '../ui.jsx'

function Board() {
  const [date, setDate] = useState(today())
  const [children] = useLoad(() => api('/children'))
  const [records, reload, loadErr] = useLoad(() => api(`/attendance?date=${date}`), [date])
  const [pick, setPick] = useState({})
  const [run, err] = useAction()
  const isToday = date === today()

  const latest = (id) => records?.find((r) => r.childId === id) // API returns newest first
  const inCount = records ? new Set(records.filter((r) => !r.checkOutAt).map((r) => r.childId)).size : 0

  const checkIn = (c) => run(() => api('/attendance/check-in', { method: 'POST', body: { childId: c.id } })).then(reload)
  const checkOut = (rec) =>
    run(() => api(`/attendance/${rec.id}/check-out`, { method: 'POST', body: { pickedUpByGuardianId: pick[rec.childId] || null } })).then(reload)

  return (
    <>
      <h1>Attendance</h1>
      <div className="toolbar">
        <label className="field inline"><span>Day</span><input type="date" value={date} max={today()} onChange={(e) => setDate(e.target.value || today())} /></label>
        {records && <span className="summary"><strong>{inCount}</strong> in now · <strong>{children?.filter((c) => c.isActive).length ?? 0}</strong> enrolled</span>}
      </div>
      <Notice>{loadErr || err}</Notice>
      {!children || !records ? null : children.length === 0 ? (
        <Empty>No children to show yet.</Empty>
      ) : (
        <section className="panel">
          {children.filter((c) => c.isActive).map((c) => {
            const rec = latest(c.id)
            const open = rec && !rec.checkOutAt
            const pickers = (c.guardians || []).filter((g) => g.canPickup)
            return (
              <div className="row" key={c.id}>
                <div className="grow">
                  <strong>{c.firstName} {c.lastName}</strong>
                  <div className="muted">{c.classroom}</div>
                </div>
                <div className="status">
                  {open ? <span className="tag ok">In since {fmtTime(rec.checkInAt)}</span>
                    : rec ? <span className="tag">Out at {fmtTime(rec.checkOutAt)}{rec.pickedUpBy ? ` · ${rec.pickedUpBy}` : ''}</span>
                    : <span className="tag off">Not in</span>}
                </div>
                {open && (
                  <div className="actions">
                    <select value={pick[c.id] || ''} onChange={(e) => setPick({ ...pick, [c.id]: e.target.value })} aria-label="Picked up by">
                      <option value="">Picked up by…</option>
                      {pickers.map((g) => <option key={g.guardianId} value={g.guardianId}>{g.fullName}</option>)}
                    </select>
                    <button className="btn" onClick={() => checkOut(rec)}>Check out</button>
                  </div>
                )}
                {!open && isToday && <button className="btn" onClick={() => checkIn(c)}>{rec ? 'Check in again' : 'Check in'}</button>}
              </div>
            )
          })}
        </section>
      )}
    </>
  )
}

function History() {
  const [children] = useLoad(() => api('/children'))
  const [childId, setChildId] = useState('')
  const [records, , loadErr] = useLoad(() => api(`/attendance${childId ? `?childId=${childId}` : ''}`), [childId])

  return (
    <>
      <h1>Attendance</h1>
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
      <section className="panel">
        <h2>Last 30 days</h2>
        {!records ? null : records.length === 0 ? (
          <Empty>No check-ins in the last 30 days.</Empty>
        ) : (
          <div className="table-wrap">
            <table>
              <thead><tr><th>Day</th><th>Child</th><th>Arrived</th><th>Left</th><th>Picked up by</th></tr></thead>
              <tbody>
                {records.map((r) => (
                  <tr key={r.id}>
                    <td>{fmtDate(new Date(r.checkInAt).toLocaleDateString('en-CA'))}</td>
                    <td>{r.child}</td>
                    <td>{fmtTime(r.checkInAt)}</td>
                    <td>{r.checkOutAt ? fmtTime(r.checkOutAt) : 'Still here'}</td>
                    <td>{r.pickedUpBy || ''}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </>
  )
}

export default function Attendance() {
  const { user } = useAuth()
  return user.role === 'parent' ? <History /> : <Board />
}
