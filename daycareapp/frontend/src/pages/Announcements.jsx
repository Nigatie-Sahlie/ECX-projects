import { useState } from 'react'
import { api, fmtDate } from '../api'
import { useAuth } from '../App.jsx'
import { Empty, Field, Notice, useAction, useLoad } from '../ui.jsx'

export default function Announcements() {
  const { user } = useAuth()
  const canPost = user.role !== 'parent'
  const isAdmin = user.role === 'admin'
  const [items, reload, loadErr] = useLoad(() => api('/announcements'))
  const [rooms] = useLoad(() => (canPost ? api('/classrooms') : Promise.resolve([])))
  const [f, setF] = useState({ title: '', body: '', classroomId: '' })
  const [edit, setEdit] = useState(null)
  const [showPost, setShowPost] = useState(false)
  const [run, err] = useAction()

  const post = async (e) => {
    e.preventDefault()
    const body = { title: f.title, body: f.body, classroomId: f.classroomId || null }
    if (await run(() => api('/announcements', { method: 'POST', body }))) {
      setF({ title: '', body: '', classroomId: f.classroomId })
      setShowPost(false)
      reload()
    }
  }
  const save = async (e) => {
    e.preventDefault()
    const body = { title: edit.title, body: edit.body, classroomId: edit.classroomId || null }
    if (await run(() => api(`/announcements/${edit.id}`, { method: 'PUT', body }))) { setEdit(null); reload() }
  }
  const remove = async (id) => {
    if (window.confirm('Delete this announcement?') && await run(() => api(`/announcements/${id}`, { method: 'DELETE' }))) reload()
  }

  return (
    <>
      <h1>Announcements</h1>
      <Notice>{loadErr}</Notice>
      {canPost && (
        <>
          <button className="btn" onClick={() => setShowPost(!showPost)}>{showPost ? 'Close announcement form' : 'Post announcement'}</button>
          {showPost && <section className="panel">
          <h2>Post an announcement</h2>
          <form className="stack" onSubmit={post}>
            <Field label="Send to">
              <select required={!isAdmin} value={f.classroomId} onChange={(e) => setF({ ...f, classroomId: e.target.value })}>
                {isAdmin ? <option value="">Whole daycare</option> : <option value="">Choose a classroom…</option>}
                {rooms?.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
              </select>
            </Field>
            <Field label="Title"><input required value={f.title} onChange={(e) => setF({ ...f, title: e.target.value })} /></Field>
            <Field label="Message"><textarea required rows={4} value={f.body} onChange={(e) => setF({ ...f, body: e.target.value })} /></Field>
            <div><button className="btn">Post announcement</button></div>
          </form>
          <Notice>{err}</Notice>
          </section>}
        </>
      )}

      {!items ? null : items.length === 0 ? (
        <Empty>No announcements yet.</Empty>
      ) : (
        items.map((a) => (
          <section className="panel" key={a.id}>
            {edit?.id === a.id ? <form className="stack" onSubmit={save}>
              <Field label="Send to"><select required={!isAdmin} value={edit.classroomId || ''} onChange={(e) => setEdit({ ...edit, classroomId: e.target.value })}>{isAdmin && <option value="">Whole daycare</option>}{rooms?.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}</select></Field>
              <Field label="Title"><input required value={edit.title} onChange={(e) => setEdit({ ...edit, title: e.target.value })} /></Field>
              <Field label="Message"><textarea required rows={4} value={edit.body} onChange={(e) => setEdit({ ...edit, body: e.target.value })} /></Field>
              <div className="actions"><button className="btn">Save changes</button><button type="button" className="btn ghost" onClick={() => setEdit(null)}>Cancel</button></div>
            </form> : <h2>{a.title}</h2>}
            <div className="muted">
              {a.author} · {fmtDate(new Date(a.publishedAt).toLocaleDateString('en-CA'))} · {a.classroom ? a.classroom : 'Whole daycare'}
            </div>
            <p className="pre">{a.body}</p>
            {(isAdmin || (user.role === 'staff' && a.author === user.fullName)) && edit?.id !== a.id && <div className="actions"><button className="btn ghost" onClick={() => setEdit({ id: a.id, title: a.title, body: a.body, classroomId: a.classroomId || rooms?.find((r) => r.name === a.classroom)?.id || '' })}>Edit</button><button className="btn ghost danger" onClick={() => remove(a.id)}>Delete</button></div>}
          </section>
        ))
      )}
    </>
  )
}
