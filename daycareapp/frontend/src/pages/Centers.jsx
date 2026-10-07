import { useState } from 'react'
import { api } from '../api'
import { Empty, Field, Notice, useAction, useLoad } from '../ui.jsx'

const blank = { name: '', address: '', phone: '', adminName: '', adminEmail: '', adminPassword: '' }

export default function Centers() {
  const [centers, reload, loadErr] = useLoad(() => api('/centers'))
  const [f, setF] = useState(blank)
  const [run, err] = useAction()
  const set = (k) => (e) => setF({ ...f, [k]: e.target.value })

  const add = async (e) => {
    e.preventDefault()
    if (await run(() => api('/centers', { method: 'POST', body: f }))) {
      setF(blank)
      reload()
    }
  }
  const toggle = (c) =>
    run(() => api(`/centers/${c.id}/active`, { method: 'PUT', body: { isActive: !c.isActive } })).then(reload)

  return (
    <>
      <h1>Daycares</h1>
      <Notice>{loadErr}</Notice>
      <section className="panel">
        <h2>Add a daycare</h2>
        <form className="grid" onSubmit={add}>
          <Field label="Daycare name"><input required value={f.name} onChange={set('name')} /></Field>
          <Field label="Address"><input value={f.address} onChange={set('address')} /></Field>
          <Field label="Phone"><input value={f.phone} onChange={set('phone')} /></Field>
          <Field label="Admin full name"><input required value={f.adminName} onChange={set('adminName')} /></Field>
          <Field label="Admin email"><input type="email" required value={f.adminEmail} onChange={set('adminEmail')} /></Field>
          <Field label="Admin password (6+ characters)">
            <input type="password" required minLength={6} value={f.adminPassword} onChange={set('adminPassword')} />
          </Field>
          <div className="full"><button className="btn">Create daycare and admin</button></div>
        </form>
        <Notice>{err}</Notice>
      </section>

      <section className="panel">
        <h2>All daycares</h2>
        {!centers ? null : centers.length === 0 ? (
          <Empty>No daycares yet. Add the first one above.</Empty>
        ) : (
          <div className="table-wrap">
            <table>
              <thead><tr><th>Name</th><th>Address</th><th>Phone</th><th>Children</th><th>Status</th><th></th></tr></thead>
              <tbody>
                {centers.map((c) => (
                  <tr key={c.id}>
                    <td>{c.name}</td>
                    <td>{c.address}</td>
                    <td>{c.phone}</td>
                    <td>{c.children}</td>
                    <td><span className={`tag ${c.isActive ? 'ok' : 'off'}`}>{c.isActive ? 'Active' : 'Disabled'}</span></td>
                    <td><button className="btn ghost" onClick={() => toggle(c)}>{c.isActive ? 'Disable' : 'Enable'}</button></td>
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
