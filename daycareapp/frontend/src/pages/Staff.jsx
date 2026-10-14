import { useState } from 'react'
import { api } from '../api'
import { ClassroomPicker, Empty, Field, Modal, Notice, Pagination, PasswordField, SearchBox, useAction, useLoad } from '../ui.jsx'

const blank = { fullName: '', email: '', password: '', phone: '', jobTitle: '', classroomIds: [] }

export default function Staff() {
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const pageSize = 10
  const [result, reload, loadErr] = useLoad(
    () => api(`/staff?search=${encodeURIComponent(search)}&page=${page}&pageSize=${pageSize}`),
    [search, page]
  )
  const [stats, , statsErr] = useLoad(() => api('/dashboard'))
  const staff = result?.items ?? result ?? []
  const total = result?.total ?? staff.length
  const [rooms] = useLoad(() => api('/classrooms'))
  const [f, setF] = useState(blank)
  const [edit, setEdit] = useState(null)
  const [showAdd, setShowAdd] = useState(false)
  const [run, err] = useAction()
  const set = (k) => (e) => setF({ ...f, [k]: e.target.value })

  const add = async (e) => {
    e.preventDefault()
    if (await run(() => api('/staff', { method: 'POST', body: f }))) {
      setF(blank)
      setShowAdd(false)
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
      <div className="page-heading">
        <h1>Staff</h1>
        <button className="btn" onClick={() => setShowAdd(true)}>Add teacher</button>
      </div>
      <Notice>{loadErr}</Notice>
      <Notice>{statsErr}</Notice>
      <section className="dashboard-cards">
        {[
          ['Staff members', stats?.staff ?? total],
          ['Classrooms', stats?.classrooms ?? 0],
          ['Children', stats?.enrolled ?? stats?.activeChildren ?? 0],
        ].map(([label, value]) => <div className="panel metric" key={label}><span>{label}</span><strong>{value}</strong></div>)}
        </section>
        {showAdd && <Modal title="Add a teacher" onClose={() => setShowAdd(false)}>
        <form className="grid" onSubmit={add}>
          <Field label="Full name"><input required value={f.fullName} onChange={set('fullName')} /></Field>
          <Field label="Email"><input type="email" required value={f.email} onChange={set('email')} /></Field>
          <PasswordField label="Password (6+ characters)" required minLength={6} value={f.password} onChange={set('password')} />
          <Field label="Phone"><input value={f.phone} onChange={set('phone')} /></Field>
          <Field label="Job title"><input value={f.jobTitle} onChange={set('jobTitle')} /></Field>
          <div className="full">
            <span className="label">Classrooms they teach</span>
            <ClassroomPicker classrooms={rooms} value={f.classroomIds} onChange={(ids) => setF({ ...f, classroomIds: ids })} />
          </div>
          <div className="full"><button className="btn">Add teacher</button></div>
        </form>
        <Notice>{err}</Notice>
      </Modal>}

      <section className="panel">
        <h2>Your team</h2>
        <div className="toolbar">
          <SearchBox value={search} onChange={(value) => { setSearch(value); setPage(1) }} placeholder="Name or email" />
        </div>
        {!staff ? null : staff.length === 0 ? (
          <Empty>{search ? 'No teachers match your search.' : 'No teachers yet. Add one above so they can check children in.'}</Empty>
        ) : (
          staff.map((s) => (
            <div className="row" key={s.id}>
              <div className="grow">
                <strong>{s.fullName}</strong> {!s.isActive && <span className="tag off">Inactive</span>}
                <div className="muted">{[s.jobTitle, s.email, s.phone].filter(Boolean).join(' · ')}</div>
                {edit?.id === s.id ? (
                  <Modal title={`Edit ${s.fullName}`} onClose={() => setEdit(null)} wide>
                    <div className="stack">
                    <div className="grid">
                      <Field label="Full name"><input required value={edit.fullName} onChange={(e) => setEdit({ ...edit, fullName: e.target.value })} /></Field>
                      <Field label="Email"><input required type="email" value={edit.email} onChange={(e) => setEdit({ ...edit, email: e.target.value })} /></Field>
                      <Field label="Phone"><input value={edit.phone} onChange={(e) => setEdit({ ...edit, phone: e.target.value })} /></Field>
                      <Field label="Job title"><input value={edit.jobTitle} onChange={(e) => setEdit({ ...edit, jobTitle: e.target.value })} /></Field>
                      <PasswordField label="New password (optional)" minLength={6} value={edit.password} onChange={(e) => setEdit({ ...edit, password: e.target.value })} />
                    </div>
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
                  </Modal>
                ) : (
                  <div className="chips">
                    {s.classroomIds.length === 0 && <span className="muted">No classrooms assigned</span>}
                    {s.classroomIds.map((id) => <span className="chip on" key={id}>{roomName(id)}</span>)}
                  </div>
                )}
              </div>
              {edit?.id !== s.id && (
                <button className="btn ghost" onClick={() => setEdit({
                  id: s.id, fullName: s.fullName, email: s.email, phone: s.phone || '',
                  jobTitle: s.jobTitle || '', password: '', isActive: s.isActive, classroomIds: s.classroomIds,
                })}>Edit</button>
              )}
            </div>
          ))
        )}
        <Pagination page={page} pageSize={pageSize} total={total} onPageChange={setPage} />
      </section>
    </>
  )
}
