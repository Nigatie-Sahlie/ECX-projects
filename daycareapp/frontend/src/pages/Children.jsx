import { useState } from 'react'
import { api, age } from '../api'
import { useAuth } from '../App.jsx'
import { Empty, Field, Modal, Notice, Pagination, PasswordField, SearchBox, useAction, useLoad } from '../ui.jsx'

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
  const [editing, setEditing] = useState(null)
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
      {child.guardians.map((guardian) => (
        <div className="row" key={`guardian-${guardian.guardianId}`}>
          <div className="grow"><strong>{guardian.fullName}</strong><div className="muted">{guardian.relationship} · {guardian.phone || guardian.email || 'No contact details'}</div></div>
          <button type="button" className="btn ghost" onClick={() => setEditing({ guardianId: guardian.guardianId, fullName: guardian.fullName, phone: guardian.phone || '', email: guardian.email || '', relationship: guardian.relationship || 'Mother', isPrimary: !!guardian.isPrimary, canPickup: guardian.canPickup !== false })}>Edit</button>
        </div>
      ))}
      <form className="grid" onSubmit={doCreate}>
        <h3 className="full">Add a new guardian</h3>
        <Field label="Full name"><input required value={g.fullName} onChange={(e) => setG({ ...g, fullName: e.target.value })} /></Field>
        <Field label="Phone"><input value={g.phone} onChange={(e) => setG({ ...g, phone: e.target.value })} /></Field>
        <Field label="Email"><input type="email" value={g.email} onChange={(e) => setG({ ...g, email: e.target.value })} /></Field>
        <PasswordField label="Password (fill in to give them an app login)" minLength={6} value={g.password} onChange={(e) => setG({ ...g, password: e.target.value })} />
        <Field label="Relationship"><Relation value={g.relationship} onChange={(v) => setG({ ...g, relationship: v })} /></Field>
        <label className="check"><input type="checkbox" checked={g.canPickup} onChange={(e) => setG({ ...g, canPickup: e.target.checked })} />Can pick up</label>
        <label className="check"><input type="checkbox" checked={g.isPrimary} onChange={(e) => setG({ ...g, isPrimary: e.target.checked })} />Primary contact</label>
        <div className="full"><button className="btn">Add guardian</button></div>
      </form>
      {child.guardians.map((guardian) => editing?.guardianId === guardian.guardianId && (
        <form className="grid" key={`edit-${guardian.guardianId}`} onSubmit={async (e) => {
          e.preventDefault()
          if (await run(() => api(`/guardians/${guardian.guardianId}`, { method: 'PUT', body: editing }))) { setEditing(null); reload(); reloadGuardians() }
        }}>
          <h3 className="full">Edit guardian</h3>
          <Field label="Full name"><input required value={editing.fullName} onChange={(e) => setEditing({ ...editing, fullName: e.target.value })} /></Field>
          <Field label="Phone"><input value={editing.phone || ''} onChange={(e) => setEditing({ ...editing, phone: e.target.value })} /></Field>
          <Field label="Email"><input type="email" value={editing.email || ''} onChange={(e) => setEditing({ ...editing, email: e.target.value })} /></Field>
          <Field label="Relationship"><Relation value={editing.relationship} onChange={(v) => setEditing({ ...editing, relationship: v })} /></Field>
          <label className="check"><input type="checkbox" checked={editing.canPickup} onChange={(e) => setEditing({ ...editing, canPickup: e.target.checked })} />Can pick up</label>
          <label className="check"><input type="checkbox" checked={editing.isPrimary} onChange={(e) => setEditing({ ...editing, isPrimary: e.target.checked })} />Primary contact</label>
          <div className="actions"><button className="btn">Save guardian</button><button type="button" className="btn ghost" onClick={() => setEditing(null)}>Cancel</button></div>
        </form>
      ))}
    </div>
  )
}

export default function Children() {
  const { user } = useAuth()
  const isAdmin = user.role === 'admin'
  const [stats, , statsErr] = useLoad(() => api('/dashboard'))
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const pageSize = 10
  const [classroomId, setClassroomId] = useState('')
  const [staffId, setStaffId] = useState('')
  const [active, setActive] = useState('')
  const [childrenResult, reload, loadErr] = useLoad(() => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (search) params.set('search', search)
    if (classroomId) params.set('classroomId', classroomId)
    if (staffId) params.set('staffId', staffId)
    if (active) params.set('active', active)
    return api(`/children?${params}`)
  }, [search, classroomId, staffId, active, page])
  const children = childrenResult?.items ?? childrenResult
  const total = childrenResult?.total ?? children?.length ?? 0
  const [rooms] = useLoad(() => api('/classrooms'))
  const [staff] = useLoad(() => (isAdmin ? api('/staff') : Promise.resolve([])))
  const [guardians, reloadGuardians] = useLoad(() => (isAdmin ? api('/guardians') : Promise.resolve([])))
  const [f, setF] = useState(blank)
  const [manage, setManage] = useState(null)
  const [edit, setEdit] = useState(null)
  const [showEnrollment, setShowEnrollment] = useState(false)
  const [details, setDetails] = useState(null)
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
  const save = async (e) => {
    e.preventDefault()
    if (await run(() => api(`/children/${edit.id}`, { method: 'PUT', body: { ...edit } }))) { setManage(null); setEdit(null); reload() }
  }
  const openManage = (c) => {
    setManage(c)
    setEdit({ id: c.id, firstName: c.firstName, lastName: c.lastName, dateOfBirth: c.dateOfBirth?.slice(0, 10), classroomId: c.classroomId, allergies: c.allergies || '', medicalNotes: c.medicalNotes || '', isActive: c.isActive })
  }
  return (
    <>
      <h1>Children</h1>
      <Notice>{loadErr}</Notice>
      <Notice>{statsErr}</Notice>
      <section className="dashboard-cards">
        {[
          ['Enrolled', stats?.enrolled ?? stats?.activeChildren ?? 0],
          ['Present now', stats?.presentNow ?? stats?.checkedIn ?? 0],
          ['Checked out today', stats?.checkedOutToday ?? 0],
          ['Reports not shared', stats?.reportsNotSharedToday ?? 0],
        ].map(([label, value]) => <div className="panel metric" key={label}><span>{label}</span><strong>{value}</strong></div>)}
      </section>
      {isAdmin && (
        <>
          <button className="btn" onClick={() => setShowEnrollment(true)}>Enroll child</button>
          {showEnrollment && <Modal title="Enroll a child" onClose={() => setShowEnrollment(false)}>
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
          </Modal>}
        </>
      )}

      <section className="panel">
        <h2>{isAdmin ? 'All children' : 'Children in your classrooms'}</h2>
        <div className="toolbar">
          <SearchBox value={search} onChange={(v) => { setSearch(v); setPage(1) }} placeholder="Name or child" />
          <Field label="Classroom">
            <select value={classroomId} onChange={(e) => { setClassroomId(e.target.value); setPage(1) }}>
              <option value="">All classrooms</option>
              {rooms?.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
            </select>
          </Field>
          {isAdmin && <Field label="Staff">
            <select value={staffId} onChange={(e) => { setStaffId(e.target.value); setPage(1) }}>
              <option value="">All staff</option>
              {(staff?.items ?? staff)?.map((s) => <option key={s.id} value={s.id}>{s.fullName}</option>)}
            </select>
          </Field>}
          <Field label="Status">
            <select value={active} onChange={(e) => { setActive(e.target.value); setPage(1) }}>
              <option value="">All children</option>
              <option value="true">Active only</option>
              <option value="false">Inactive only</option>
            </select>
          </Field>
        </div>
        {!children ? null : children.length === 0 ? (
          <Empty>{isAdmin ? 'No children enrolled yet.' : 'No children in your classrooms yet. Ask your admin to assign you a classroom.'}</Empty>
        ) : (
          children.map((c) => (
            <div className="card" key={c.id}>
              <div className="row">
                <div className="grow">
                  <strong>{c.firstName} {c.lastName}</strong> {!c.isActive && <span className="tag off">Inactive</span>}
                  <div className="muted">{age(c.dateOfBirth)} · {c.classroom}</div>
                  {c.staff?.length > 0 && <div className="muted">Staff: {c.staff.map((s) => s.name).filter(Boolean).join(', ')}</div>}
                </div>
                <button className="btn ghost" onClick={() => setDetails(details === c.id ? null : c.id)}>
                  {details === c.id ? 'Hide details' : 'View details'}
                </button>
                {isAdmin && (
                  <div className="actions">
                    <button className="btn ghost" onClick={() => openManage(c)}>Manage child</button>
                    <button className="btn ghost" onClick={() => toggleActive(c)}>{c.isActive ? 'Deactivate' : 'Reactivate'}</button>
                  </div>
                )}
              </div>
              {details === c.id && <Modal title={`${c.firstName} ${c.lastName} details`} onClose={() => setDetails(null)}>
                {c.allergies && <div><span className="tag warn">Allergies: {c.allergies}</span></div>}
                {c.medicalNotes && <div className="muted">Medical notes: {c.medicalNotes}</div>}
                {!c.allergies && !c.medicalNotes && <div className="muted">No medical or allergy notes.</div>}
                <strong>Guardians</strong>
                <ul className="plain">
                {c.guardians.length === 0 && <li className="muted">No guardians linked yet.</li>}
                {c.guardians.map((g) => (
                  <li key={g.guardianId}>
                    {g.fullName} ({g.relationship}){g.isPrimary && ' · primary'}{!g.canPickup && ' · no pickup'}
                    {g.phone && ` · ${g.phone}`}
                    {isAdmin && <><button className="link" onClick={() => openManage(c)}>Edit</button><button className="link" onClick={() => unlink(c, g.guardianId)}>Remove</button></>}
                  </li>
                ))}
                </ul>
              </Modal>}
              {isAdmin && manage?.id === c.id && <Modal title={`Manage ${c.firstName} ${c.lastName}`} onClose={() => { setManage(null); setEdit(null) }} wide>
                <div className="manage-sections">
                  <form className="grid" onSubmit={save}>
                    <h3 className="full">Child details</h3>
                    <Field label="First name"><input required value={edit.firstName} onChange={(e) => setEdit({ ...edit, firstName: e.target.value })} /></Field>
                    <Field label="Last name"><input required value={edit.lastName} onChange={(e) => setEdit({ ...edit, lastName: e.target.value })} /></Field>
                    <Field label="Date of birth"><input required type="date" value={edit.dateOfBirth} onChange={(e) => setEdit({ ...edit, dateOfBirth: e.target.value })} /></Field>
                    <Field label="Classroom"><select required value={edit.classroomId} onChange={(e) => setEdit({ ...edit, classroomId: e.target.value })}>{rooms?.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}</select></Field>
                    <Field label="Allergies"><input value={edit.allergies} onChange={(e) => setEdit({ ...edit, allergies: e.target.value })} /></Field>
                    <Field label="Medical notes"><input value={edit.medicalNotes} onChange={(e) => setEdit({ ...edit, medicalNotes: e.target.value })} /></Field>
                    <div className="actions full"><button className="btn">Save child details</button></div>
                  </form>
                  <div>
                    <h3>Guardians</h3>
                    <GuardianPanel child={c} guardians={guardians} reload={reload} reloadGuardians={reloadGuardians} />
                  </div>
                </div>
              </Modal>}
            </div>
          ))
        )}
        <Pagination page={page} pageSize={pageSize} total={total} onPageChange={setPage} />
      </section>
    </>
  )
}
