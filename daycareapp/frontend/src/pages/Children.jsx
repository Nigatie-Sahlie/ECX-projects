import { useState } from 'react'
import { api, age } from '../api'
import { useAuth } from '../App.jsx'
import { Empty, Field, Notice, useAction, useLoad } from '../ui.jsx'

const blank = { firstName: '', lastName: '', dateOfBirth: '', classroomId: '', allergies: '', medicalNotes: '' }
const RELATIONS = ['Mother', 'Father', 'Grandparent', 'Other guardian']

function Relation({ value, onChange }) {
  return (
    <select value={value} onChange={(e) => onChange(e.target.value)}>
      {RELATIONS.map((r) => <option key={r}>{r}</option>)}
    </select>
  )
}

function GuardianPanel({ child, guardians, reload, reloadGuardians }) {
  const [run, err] = useAction()
  const [link, setLink] = useState({ guardianId: '', relationship: 'Mother', isPrimary: false, canPickup: true })
  const fresh = { fullName: '', phone: '', email: '', password: '', relationship: 'Mother', isPrimary: false, canPickup: true }
  const [g, setG] = useState(fresh)
  const linked = new Set(child.guardians.map((x) => x.guardianId))
  const available = (guardians || []).filter((x) => !linked.has(x.id))

  const doLink = async (e) => {
    e.preventDefault()
    if (await run(() => api(`/children/${child.id}/guardians`, { method: 'POST', body: link }))) {
      setLink({ ...link, guardianId: '' })
      reload()
    }
  }
  const doCreate = async (e) => {
    e.preventDefault()
    if (await run(() => api('/guardians', { method: 'POST', body: { ...g, childId: child.id } }))) {
      setG(fresh)
      reload()
      reloadGuardians()
    }
  }

  return (
    <div className="subpanel">
      <Notice>{err}</Notice>
      {available.length > 0 && (
        <form className="grid" onSubmit={doLink}>
          <h3 className="full">Link an existing guardian</h3>
          <Field label="Guardian">
            <select required value={link.guardianId} onChange={(e) => setLink({ ...link, guardianId: e.target.value })}>
              <option value="">Choose…</option>
              {available.map((x) => <option key={x.id} value={x.id}>{x.fullName}</option>)}
            </select>
          </Field>
          <Field label="Relationship"><Relation value={link.relationship} onChange={(v) => setLink({ ...link, relationship: v })} /></Field>
          <label className="check"><input type="checkbox" checked={link.canPickup} onChange={(e) => setLink({ ...link, canPickup: e.target.checked })} />Can pick up</label>
          <label className="check"><input type="checkbox" checked={link.isPrimary} onChange={(e) => setLink({ ...link, isPrimary: e.target.checked })} />Primary contact</label>
          <div className="full"><button className="btn">Link guardian</button></div>
        </form>
      )}
      <form className="grid" onSubmit={doCreate}>
        <h3 className="full">Add a new guardian</h3>
        <Field label="Full name"><input required value={g.fullName} onChange={(e) => setG({ ...g, fullName: e.target.value })} /></Field>
        <Field label="Phone"><input value={g.phone} onChange={(e) => setG({ ...g, phone: e.target.value })} /></Field>
        <Field label="Email"><input type="email" value={g.email} onChange={(e) => setG({ ...g, email: e.target.value })} /></Field>
        <Field label="Password (fill in to give them an app login)">
          <input type="password" minLength={6} value={g.password} onChange={(e) => setG({ ...g, password: e.target.value })} />
        </Field>
        <Field label="Relationship"><Relation value={g.relationship} onChange={(v) => setG({ ...g, relationship: v })} /></Field>
        <label className="check"><input type="checkbox" checked={g.canPickup} onChange={(e) => setG({ ...g, canPickup: e.target.checked })} />Can pick up</label>
        <label className="check"><input type="checkbox" checked={g.isPrimary} onChange={(e) => setG({ ...g, isPrimary: e.target.checked })} />Primary contact</label>
        <div className="full"><button className="btn">Add guardian</button></div>
      </form>
    </div>
  )
}

export default function Children() {
  const { user } = useAuth()
  const isAdmin = user.role === 'admin'
  const [children, reload, loadErr] = useLoad(() => api('/children'))
  const [rooms] = useLoad(() => api('/classrooms'))
  const [guardians, reloadGuardians] = useLoad(() => (isAdmin ? api('/guardians') : Promise.resolve([])))
  const [f, setF] = useState(blank)
  const [open, setOpen] = useState(null)
  const [run, err] = useAction()
  const set = (k) => (e) => setF({ ...f, [k]: e.target.value })

  const add = async (e) => {
    e.preventDefault()
    if (await run(() => api('/children', { method: 'POST', body: f }))) {
      setF(blank)
      reload()
    }
  }
  const toggleActive = (c) =>
    run(() => api(`/children/${c.id}`, {
      method: 'PUT',
      body: { firstName: c.firstName, lastName: c.lastName, dateOfBirth: c.dateOfBirth, classroomId: c.classroomId,
              allergies: c.allergies, medicalNotes: c.medicalNotes, isActive: !c.isActive },
    })).then(reload)
  const unlink = (c, gid) => run(() => api(`/children/${c.id}/guardians/${gid}`, { method: 'DELETE' })).then(reload)

  return (
    <>
      <h1>Children</h1>
      <Notice>{loadErr}</Notice>
      {isAdmin && (
        <section className="panel">
          <h2>Enroll a child</h2>
          {rooms && rooms.length === 0 ? (
            <Empty>Add a classroom first (Classrooms page), then enroll children.</Empty>
          ) : (
            <form className="grid" onSubmit={add}>
              <Field label="First name"><input required value={f.firstName} onChange={set('firstName')} /></Field>
              <Field label="Last name"><input required value={f.lastName} onChange={set('lastName')} /></Field>
              <Field label="Date of birth"><input type="date" required value={f.dateOfBirth} onChange={set('dateOfBirth')} /></Field>
              <Field label="Classroom">
                <select required value={f.classroomId} onChange={set('classroomId')}>
                  <option value="">Choose…</option>
                  {rooms?.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
                </select>
              </Field>
              <Field label="Allergies"><input value={f.allergies} onChange={set('allergies')} /></Field>
              <Field label="Medical notes"><input value={f.medicalNotes} onChange={set('medicalNotes')} /></Field>
              <div className="full"><button className="btn">Enroll child</button></div>
            </form>
          )}
          <Notice>{err}</Notice>
        </section>
      )}

      <section className="panel">
        <h2>{isAdmin ? 'All children' : 'Children in your classrooms'}</h2>
        {!children ? null : children.length === 0 ? (
          <Empty>{isAdmin ? 'No children enrolled yet.' : 'No children in your classrooms yet. Ask your admin to assign you a classroom.'}</Empty>
        ) : (
          children.map((c) => (
            <div className="card" key={c.id}>
              <div className="row">
                <div className="grow">
                  <strong>{c.firstName} {c.lastName}</strong> {!c.isActive && <span className="tag off">Inactive</span>}
                  <div className="muted">{age(c.dateOfBirth)} · {c.classroom}</div>
                  {c.allergies && <span className="tag warn">Allergies: {c.allergies}</span>}
                  {c.medicalNotes && <div className="muted">Medical: {c.medicalNotes}</div>}
                </div>
                {isAdmin && (
                  <div className="actions">
                    <button className="btn ghost" onClick={() => setOpen(open === c.id ? null : c.id)}>
                      {open === c.id ? 'Close' : 'Manage guardians'}
                    </button>
                    <button className="btn ghost" onClick={() => toggleActive(c)}>{c.isActive ? 'Deactivate' : 'Reactivate'}</button>
                  </div>
                )}
              </div>
              <ul className="plain">
                {c.guardians.length === 0 && <li className="muted">No guardians linked yet.</li>}
                {c.guardians.map((g) => (
                  <li key={g.guardianId}>
                    {g.fullName} ({g.relationship}){g.isPrimary && ' · primary'}{!g.canPickup && ' · no pickup'}
                    {g.phone && ` · ${g.phone}`}
                    {isAdmin && <button className="link" onClick={() => unlink(c, g.guardianId)}>Remove</button>}
                  </li>
                ))}
              </ul>
              {isAdmin && open === c.id && (
                <GuardianPanel child={c} guardians={guardians} reload={reload} reloadGuardians={reloadGuardians} />
              )}
            </div>
          ))
        )}
      </section>
    </>
  )
}
