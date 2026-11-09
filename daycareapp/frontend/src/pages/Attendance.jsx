import { useState } from 'react'
import { api, fmtDate, fmtTime, today } from '../api'
import { useAuth } from '../App.jsx'
import { Empty, Modal, Notice, useAction, useLoad } from '../ui.jsx'

function Board() {
  const { user } = useAuth()
  const canWrite = user.role === 'staff'
  const [date, setDate] = useState(today())
  const [children] = useLoad(() => api('/children'))
  const [records, reload, loadErr] = useLoad(() => api(`/attendance?date=${date}`), [date])
  const [pick, setPick] = useState({})
  const [details, setDetails] = useState(null)
  const [run, err] = useAction()
  const isToday = date === today()

  const latest = (id) => records?.find((r) => r.childId === id) // API returns newest first
  const inCount = records ? new Set(records.filter((r) => !r.checkOutAt).map((r) => r.childId)).size : 0
  const activeChildren = children?.filter((c) => c.isActive) ?? []
  const checkedOutCount = records ? new Set(records.filter((r) => r.checkOutAt).map((r) => r.childId)).size : 0
  const notInCount = Math.max(0, activeChildren.length - inCount - checkedOutCount)

  const checkIn = (c) => run(() => api('/attendance/check-in', { method: 'POST', body: { childId: c.id } })).then(reload)
  const checkOut = (rec) =>
    run(() => api(`/attendance/${rec.id}/check-out`, { method: 'POST', body: { pickedUpByGuardianId: pick[rec.childId] || null } })).then(reload)

  return (
    <>
      <div className="page-heading attendance-heading"><div><span className="portal-eyebrow plain">Operations core</span><h1>Daily attendance</h1><p className="muted">Monitor check-ins, pickups, and classroom safety in real time.</p></div><label className="field attendance-date"><span>Attendance date</span><input type="date" value={date} max={today()} onChange={(e) => setDate(e.target.value || today())} /></label></div>
      {records && <div className="attendance-stats"><div><span className="material-symbols-outlined">how_to_reg</span><small>Present now</small><strong>{inCount}<em> / {activeChildren.length}</em></strong></div><div><span className="material-symbols-outlined">logout</span><small>Checked out</small><strong>{checkedOutCount}</strong></div><div><span className="material-symbols-outlined">schedule</span><small>Not checked in</small><strong>{notInCount}</strong></div></div>}
      <Notice>{loadErr || err}</Notice>
      {!children || !records ? null : children.length === 0 ? (
        <Empty>No children to show yet.</Empty>
      ) : (
        <section className="panel attendance-board">
          <div className="attendance-board-header"><div><h2>Children roster</h2><p className="muted">Select a child to record a check-in or pickup.</p></div><span className="tag ok"><i /> Live room audit</span></div>
          {children.filter((c) => c.isActive).map((c) => {
            const rec = latest(c.id)
            const open = rec && !rec.checkOutAt
            const pickers = (c.guardians || []).filter((g) => g.canPickup)
            return (
              <div className="attendance-row" key={c.id}>
                <div className="child-identity"><span className="child-avatar">{c.firstName?.charAt(0)}{c.lastName?.charAt(0)}</span><div><strong>{c.firstName} {c.lastName}</strong><div className="muted">{c.classroom}</div></div>
                </div>
                <div className="attendance-status">
                  {open ? <span className="tag ok">In since {fmtTime(rec.checkInAt)}</span>
                    : rec ? <><span className="tag">Out at {fmtTime(rec.checkOutAt)}{rec.pickedUpBy ? ` · ${rec.pickedUpBy}` : ''}</span><button className="link" onClick={() => setDetails(details === rec.id ? null : rec.id)}>Details</button></>
                    : <span className="tag off">Not in</span>}
                </div>
                {open && canWrite && (
                  <div className="actions attendance-actions">
                    <select value={pick[c.id] || ''} onChange={(e) => setPick({ ...pick, [c.id]: e.target.value })} aria-label="Picked up by">
                      <option value="">Picked up by…</option>
                      {pickers.map((g) => <option key={g.guardianId} value={g.guardianId}>{g.fullName}</option>)}
                    </select>
                    <button className="btn" onClick={() => checkOut(rec)}>Check out</button>
                  </div>
                )}
                {!open && isToday && canWrite && <button className="btn attendance-action-button" onClick={() => checkIn(c)}>{rec ? 'Check in again' : 'Check in'}</button>}
                {details === rec?.id && <Modal title={`Attendance details: ${c.firstName} ${c.lastName}`} onClose={() => setDetails(null)}><strong>Pickup details</strong><div className="muted">Checked in: {fmtTime(rec.checkInAt)} by {rec.checkedInBy || 'unknown'}</div><div className="muted">Checked out: {fmtTime(rec.checkOutAt)} by {rec.checkedOutBy || 'unknown'}</div><div className="muted">Picked up by: {rec.pickedUpBy || 'Not recorded'}{rec.pickedUpByPhone ? ` · ${rec.pickedUpByPhone}` : ''}</div></Modal>}
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
      <div className="page-heading attendance-heading"><div><span className="portal-eyebrow plain">Family portal</span><h1>Attendance history</h1><p className="muted">Review recent arrival and pickup activity.</p></div>
        <label className="field attendance-date">
          <span>Child</span>
          <select value={childId} onChange={(e) => setChildId(e.target.value)}>
            <option value="">All my children</option>
            {children?.map((c) => <option key={c.id} value={c.id}>{c.firstName}</option>)}
          </select>
        </label></div>
      <Notice>{loadErr}</Notice>
      <section className="panel attendance-history">
        <div className="attendance-board-header"><div><h2>Last 30 days</h2><p className="muted">A secure record of your child&apos;s attendance.</p></div></div>
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
