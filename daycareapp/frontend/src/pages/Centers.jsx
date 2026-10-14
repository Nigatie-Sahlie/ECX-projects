import { useState } from 'react'
import { api } from '../api'
import { Empty, Field, Modal, Notice, PasswordField, SearchBox, useAction, useLoad } from '../ui.jsx'

const blank = { name: '', address: '', phone: '', adminName: '', adminEmail: '', adminPassword: '' }

export default function Centers() {
  const [centers, reload, loadErr] = useLoad(() => api('/centers'))
  const [stats, , statsErr] = useLoad(() => api('/dashboard'))
  const [adminUsers, reloadAdmins, adminUsersErr] = useLoad(() => api('/centers/admin-users'))
  const [f, setF] = useState(blank)
  const [run, err] = useAction()
  const [editingCenter, setEditingCenter] = useState(null)
  const [success, setSuccess] = useState('')
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [showRegistration, setShowRegistration] = useState(false)
  const [editingAdmin, setEditingAdmin] = useState(null)
  const set = (k) => (e) => setF({ ...f, [k]: e.target.value })
  const filteredCenters = (centers || []).filter((c) =>
    (!search || `${c.name} ${c.address || ''} ${c.phone || ''} ${c.admins?.map((a) => `${a.fullName} ${a.email}`).join(' ') || ''}`.toLowerCase().includes(search.toLowerCase())) &&
    (!status || (status === 'active' ? c.isActive : !c.isActive))
  )

  const add = async (e) => {
    e.preventDefault()
    if (await run(() => api('/centers', { method: 'POST', body: f }))) {
      setF(blank)
      setShowRegistration(false)
      setSuccess('Daycare and administrator registered successfully.')
      reload()
      reloadAdmins()
    }
  }
  const toggle = async (c) => {
    if (await run(() => api(`/centers/${c.id}/active`, { method: 'PUT', body: { isActive: !c.isActive } }))) {
      setSuccess(`Daycare ${c.isActive ? 'disabled' : 'enabled'} successfully.`); reload()
    }
  }
  const saveCenter = async (e) => {
    e.preventDefault()
    if (await run(() => api(`/centers/${editingCenter.id}`, { method: 'PUT', body: editingCenter }))) { setEditingCenter(null); setSuccess('Daycare updated successfully.'); reload() }
  }
  const saveAdmin = async (e) => {
    e.preventDefault()
    if (await run(() => api(`/centers/admin-users/${editingAdmin.id}`, { method: 'PUT', body: editingAdmin }))) {
      setEditingAdmin(null)
      setSuccess('Administrator updated successfully.')
      reloadAdmins()
      reload()
    }
  }

  return (
    <>
      <div className="page-heading">
        <h1>Centers</h1>
        <button className="btn" onClick={() => setShowRegistration(true)}>Create Center</button>
      </div>
      <Notice>{loadErr}</Notice>
      <Notice>{statsErr}</Notice>
      <Notice>{adminUsersErr}</Notice>
      <Notice kind="ok">{success}</Notice>
      <section className="dashboard-cards">
        {[['Children', stats?.children ?? 0], ['Staff', stats?.staff ?? 0], ['Daycares', stats?.centers ?? 0], ['Active daycares', stats?.activeCenters ?? 0]].map(([label, value]) => <div className="panel metric" key={label}><span>{label}</span><strong>{value}</strong></div>)}
      </section>
      {showRegistration && <Modal title="Add a daycare" onClose={() => setShowRegistration(false)} wide>
        <form className="grid" onSubmit={add}>
          <Field label="Daycare name"><input required value={f.name} onChange={set('name')} /></Field>
          <Field label="Address"><input value={f.address} onChange={set('address')} /></Field>
          <Field label="Phone"><input value={f.phone} onChange={set('phone')} /></Field>
          <Field label="Admin full name"><input required value={f.adminName} onChange={set('adminName')} /></Field>
          <Field label="Admin email"><input type="email" required value={f.adminEmail} onChange={set('adminEmail')} /></Field>
          <Field label="Admin password (6+ characters)">
            <PasswordField required minLength={6} value={f.adminPassword} onChange={set('adminPassword')} />
          </Field>
          <div className="full"><button className="btn">Create center and admin</button></div>
        </form>
        <Notice>{err}</Notice>
      </Modal>}

      <section className="panel">
        <h2>All Centers</h2>
        <div className="toolbar">
          <SearchBox value={search} onChange={setSearch} placeholder="Name, address, or admin" />
          <Field label="Status">
            <select value={status} onChange={(e) => setStatus(e.target.value)}>
              <option value="">All Centers</option>
              <option value="active">Active only</option>
              <option value="disabled">Disabled only</option>
            </select>
          </Field>
        </div>
        {!centers ? null : filteredCenters.length === 0 ? (
          <Empty>No centers yet. Add the first one above.</Empty>
        ) : (
          <div className="table-wrap">
            <table>
              <thead><tr><th>Name</th><th>Address</th><th>Phone</th><th>Children</th><th>Admin</th><th>Status</th><th></th></tr></thead>
              <tbody>
                {filteredCenters.map((c) => (
                  <tr key={c.id}>
                    <td>{c.name}</td>
                    <td>{c.address}</td>
                    <td>{c.phone}</td>
                    <td>{c.children}</td>
                    <td>{c.admins?.length ? c.admins.map((a) => `${a.fullName}${a.isActive ? '' : ' (disabled)'}`).join(', ') : <button className="link assign-admin" onClick={() => setEditingCenter({ ...c, adminId: '' })}>Assign admin</button>}</td>
                    <td><span className={`tag ${c.isActive ? 'ok' : 'off'}`}>{c.isActive ? 'Active' : 'Disabled'}</span></td>
                    <td><div className="actions center-actions"><button className="btn ghost" onClick={() => setEditingCenter({ ...c, adminId: c.admins?.[0]?.id || '' })}>Edit</button><button className="btn ghost" onClick={() => toggle(c)}>{c.isActive ? 'Disable' : 'Enable'}</button></div></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
      <section className="panel">
        <h2>Manage administrators</h2>
        <p className="muted">Edit administrator details, assign them to a centers, or enable and disable their accounts.</p>
        {adminUsersErr && <Notice>{adminUsersErr}</Notice>}
        {!adminUsers ? <p className="muted">Loading…</p> : adminUsers.length === 0 ? <Empty>No administrators registered.</Empty> : (
          <div className="table-wrap">
            <table>
              <thead><tr><th>Name</th><th>Email</th><th>Daycare</th><th>Status</th><th></th></tr></thead>
              <tbody>{adminUsers.map((a) => {
                const center = centers?.find((c) => c.id === a.centerId)
                return <tr key={a.id}>
                  <td>{a.fullName}</td>
                  <td>{a.email}</td>
                  <td>{center?.name || 'Unassigned'}</td>
                  <td><span className={`tag ${a.isActive ? 'ok' : 'off'}`}>{a.isActive ? 'Active' : 'Disabled'}</span></td>
                  <td><button className="btn ghost" onClick={() => setEditingAdmin({ ...a })}>Edit / manage</button></td>
                </tr>
              })}</tbody>
            </table>
          </div>
        )}
      </section>
      {editingCenter && <Modal title="Edit daycare" onClose={() => setEditingCenter(null)} wide><form className="grid" onSubmit={saveCenter}><Field label="Name"><input required value={editingCenter.name} onChange={(e) => setEditingCenter({ ...editingCenter, name: e.target.value })} /></Field><Field label="Address"><input value={editingCenter.address || ''} onChange={(e) => setEditingCenter({ ...editingCenter, address: e.target.value })} /></Field><Field label="Phone"><input value={editingCenter.phone || ''} onChange={(e) => setEditingCenter({ ...editingCenter, phone: e.target.value })} /></Field><Field label="Assign or reassign administrator"><select value={editingCenter.adminId} onChange={(e) => setEditingCenter({ ...editingCenter, adminId: e.target.value })}><option value="">No administrator</option>{adminUsers?.map((a) => <option key={a.id} value={a.id}>{a.fullName} · {a.email}{a.centerId && a.centerId !== editingCenter.id ? ' (assigned to another center)' : ''}</option>)}</select></Field><div><button className="btn">Save daycare</button><button type="button" className="btn ghost" onClick={() => setEditingCenter(null)}>Cancel</button></div></form><Notice>{err}</Notice></Modal>}
      {editingAdmin && <Modal title="Manage administrator" onClose={() => setEditingAdmin(null)} wide><form className="grid" onSubmit={saveAdmin}>
        <Field label="Full name"><input required value={editingAdmin.fullName} onChange={(e) => setEditingAdmin({ ...editingAdmin, fullName: e.target.value })} /></Field>
        <Field label="Email"><input required type="email" value={editingAdmin.email} onChange={(e) => setEditingAdmin({ ...editingAdmin, email: e.target.value })} /></Field>
        <Field label="Phone"><input value={editingAdmin.phone || ''} onChange={(e) => setEditingAdmin({ ...editingAdmin, phone: e.target.value })} /></Field>
        <Field label="Assign to daycare"><select required value={editingAdmin.centerId || ''} onChange={(e) => setEditingAdmin({ ...editingAdmin, centerId: e.target.value })}><option value="">Choose a daycare</option>{centers?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}</select></Field>
        <label className="check"><input type="checkbox" checked={editingAdmin.isActive} onChange={(e) => setEditingAdmin({ ...editingAdmin, isActive: e.target.checked })} />Account is active</label>
        <div><button className="btn">Save administrator</button><button type="button" className="btn ghost" onClick={() => setEditingAdmin(null)}>Cancel</button></div>
      </form><Notice>{err}</Notice></Modal>}
    </>
  )
}
