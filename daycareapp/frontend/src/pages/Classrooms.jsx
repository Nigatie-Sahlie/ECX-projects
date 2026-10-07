import { useState } from 'react'
import { api } from '../api'
import { Empty, Field, Notice, useAction, useLoad } from '../ui.jsx'

const blank = { name: '', ageGroup: '', capacity: 10 }

export default function Classrooms() {
  const [rooms, reload, loadErr] = useLoad(() => api('/classrooms'))
  const [f, setF] = useState(blank)
  const [run, err] = useAction()

  const add = async (e) => {
    e.preventDefault()
    if (await run(() => api('/classrooms', { method: 'POST', body: { ...f, capacity: Number(f.capacity) } }))) {
      setF(blank)
      reload()
    }
  }

  return (
    <>
      <h1>Classrooms</h1>
      <Notice>{loadErr}</Notice>
      <section className="panel">
        <h2>Add a classroom</h2>
        <form className="grid" onSubmit={add}>
          <Field label="Name"><input required value={f.name} onChange={(e) => setF({ ...f, name: e.target.value })} /></Field>
          <Field label="Age group"><input placeholder="e.g. 2-3 years" value={f.ageGroup} onChange={(e) => setF({ ...f, ageGroup: e.target.value })} /></Field>
          <Field label="Capacity"><input type="number" min="1" value={f.capacity} onChange={(e) => setF({ ...f, capacity: e.target.value })} /></Field>
          <div className="full"><button className="btn">Add classroom</button></div>
        </form>
        <Notice>{err}</Notice>
      </section>
      <section className="panel">
        <h2>All classrooms</h2>
        {!rooms ? null : rooms.length === 0 ? (
          <Empty>No classrooms yet. Children and teachers are assigned to classrooms, so add one first.</Empty>
        ) : (
          rooms.map((r) => (
            <div className="row" key={r.id}>
              <div className="grow"><strong>{r.name}</strong><div className="muted">{r.ageGroup} · up to {r.capacity} children</div></div>
            </div>
          ))
        )}
      </section>
    </>
  )
}
