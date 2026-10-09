import { useState } from 'react'
import { api } from '../api'
import { Empty, Field, Modal, Notice, useAction, useLoad } from '../ui.jsx'

const blank = { name: '', ageGroup: '', capacity: 10 }

export default function Classrooms() {
  const [rooms, reload, loadErr] = useLoad(() => api('/classrooms'))
  const [f, setF] = useState(blank)
  const [edit, setEdit] = useState(null)
  const [showAdd, setShowAdd] = useState(false)
  const [run, err] = useAction()

  const add = async (e) => {
    e.preventDefault()
    if (await run(() => api('/classrooms', { method: 'POST', body: { ...f, capacity: Number(f.capacity) } }))) {
      setF(blank)
      setShowAdd(false)
      reload()
    }
  }
  const save = async (e) => {
    e.preventDefault()
    if (await run(() => api(`/classrooms/${edit.id}`, { method: 'PUT', body: {
      name: edit.name, ageGroup: edit.ageGroup, capacity: Number(edit.capacity),
    } }))) {
      setEdit(null); reload()
    }
  }

  return (
    <>
      <h1>Classrooms</h1>
      <Notice>{loadErr}</Notice>
      <button className="btn" onClick={() => setShowAdd(true)}>Add classroom</button>
      {showAdd && <Modal title="Add a classroom" onClose={() => setShowAdd(false)}>
        <form className="grid" onSubmit={add}>
          <Field label="Name"><input required value={f.name} onChange={(e) => setF({ ...f, name: e.target.value })} /></Field>
          <Field label="Age group"><input placeholder="e.g. 2-3 years" value={f.ageGroup} onChange={(e) => setF({ ...f, ageGroup: e.target.value })} /></Field>
          <Field label="Capacity"><input type="number" min="1" value={f.capacity} onChange={(e) => setF({ ...f, capacity: e.target.value })} /></Field>
          <div className="full"><button className="btn">Add classroom</button></div>
        </form>
        <Notice>{err}</Notice>
      </Modal>}
      <section className="panel">
        <h2>All classrooms</h2>
        {!rooms ? null : rooms.length === 0 ? (
          <Empty>No classrooms yet. Children and teachers are assigned to classrooms, so add one first.</Empty>
        ) : (
          rooms.map((r) => (
            <div className="row" key={r.id}>
              <div className="grow">
                {edit?.id === r.id ? (
                  <Modal title={`Edit ${r.name}`} onClose={() => setEdit(null)}>
                    <form className="grid" onSubmit={save}>
                    <Field label="Name"><input required value={edit.name} onChange={(e) => setEdit({ ...edit, name: e.target.value })} /></Field>
                    <Field label="Age group"><input value={edit.ageGroup || ''} onChange={(e) => setEdit({ ...edit, ageGroup: e.target.value })} /></Field>
                    <Field label="Capacity"><input required type="number" min="1" value={edit.capacity} onChange={(e) => setEdit({ ...edit, capacity: e.target.value })} /></Field>
                    <div className="actions"><button className="btn">Save</button><button type="button" className="btn ghost" onClick={() => setEdit(null)}>Cancel</button></div>
                    </form>
                  </Modal>
                ) : <><strong>{r.name}</strong><div className="muted">{r.ageGroup} · up to {r.capacity} children</div></>}
              </div>
              {edit?.id !== r.id && <button className="btn ghost" onClick={() => setEdit({ id: r.id, name: r.name, ageGroup: r.ageGroup, capacity: r.capacity })}>Edit</button>}
            </div>
          ))
        )}
      </section>
    </>
  )
}
