import { useState } from 'react'
import { api } from '../api'
import { ClassroomPicker, Empty, Field, Notice, useAction, useLoad } from '../ui.jsx'

const blank = { fullName: '', email: '', password: '', phone: '', jobTitle: '', classroomIds: [] }

export default function Staff() {
  const [staff, reload, loadErr] = useLoad(() => api('/staff'))
  const [rooms] = useLoad(() => api('/classrooms'))
  const [f, setF] = useState(blank)
  const [edit, setEdit] = useState(null) // { id, isActive, classroomIds }
  const [run, err] = useAction()
  const set = (k) => (e) => setF({ ...f, [k]: e.target.value })

  const add = async (e) => {
    e.preventDefault()
    if (await run(() => api('/staff', { method: 'POST', body: f }))) {
      setF(blank)
      reload()
    }
  }
  const save = async () => {
    if (await run(() => api(`/staff/${edit.id}`, { method: 'PUT', body: edit }))) {
      setEdit(null)
      reload()
    }
  }
  const roomName = (id) => rooms?.find((r) => r.id === id)?.name

  return (
    <>
      <h1>Staff</h1>
      <Notice>{loadErr}</Notice>
      <section className="panel">
        <h2>Add a teacher</h2>
        <form className="grid" onSubmit={add}>
          <Field label="Full name"><input required value={f.fullName} onChange={set('fullName')} /></Field>
          <Field label="Email"><input type="email" required value={f.email} onChange={set('email')} /></Field>
          <Field label="Password (6+ characters)"><input type="password" required minLength={6} value={f.password} onChange={set('password')} /></Field>
          <Field label="Phone"><input value={f.phone} onChange={set('phone')} /></Field>
          <Field label="Job title"><input value={f.jobTitle} onChange={set('jobTitle')} /></Field>
          <div className="full">
            <span className="label">Classrooms they teach</span>
            <ClassroomPicker classrooms={rooms} value={f.classroomIds} onChange={(ids) => setF({ ...f, classroomIds: ids })} />
          </div>
          <div className="full"><button className="btn">Add teacher</button></div>
        </form>
        <Notice>{err}</Notice>
      </section>

      <section className="panel">
        <h2>Your team</h2>
        {!staff ? null : staff.length === 0 ? (
          <Empty>No teachers yet. Add one above so they can check children in.</Empty>
        ) : (
          staff.map((s) => (
            <div className="row" key={s.id}>
              <div className="grow">
                <strong>{s.fullName}</strong> {!s.isActive && <span className="tag off">Inactive</span>}
                <div className="muted">{[s.jobTitle, s.email, s.phone].filter(Boolean).join(' · ')}</div>
                {edit?.id === s.id ? (
                  <div className="stack">
                    <ClassroomPicker classrooms={rooms} value={edit.classroomIds} onChange={(ids) => setEdit({ ...edit, classroomIds: ids })} />
                    <label className="check">
                      <input type="checkbox" checked={edit.isActive} onChange={(e) => setEdit({ ...edit, isActive: e.target.checked })} />
                      Account is active
                    </label>
                    <div className="actions">
                      <button className="btn" onClick={save}>Save changes</button>
                      <button className="btn ghost" onClick={() => setEdit(null)}>Cancel</button>
                    </div>
                  </div>
                ) : (
                  <div className="chips">
                    {s.classroomIds.length === 0 && <span className="muted">No classrooms assigned</span>}
                    {s.classroomIds.map((id) => <span className="chip on" key={id}>{roomName(id)}</span>)}
                  </div>
                )}
              </div>
              {edit?.id !== s.id && (
                <button className="btn ghost" onClick={() => setEdit({ id: s.id, isActive: s.isActive, classroomIds: s.classroomIds })}>Edit</button>
              )}
            </div>
          ))
        )}
      </section>
    </>
  )
}
