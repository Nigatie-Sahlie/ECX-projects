import { useEffect, useState } from 'react'
import { api, fmtDate, fmtTime } from '../api'
import { useAuth } from '../App.jsx'
import { Empty, Field, Modal, Notice, useAction, useLoad } from '../ui.jsx'

export default function Messages() {
  const { user, markMessageNotificationsRead } = useAuth()
  const [conversations, reload, loadErr] = useLoad(() => api('/messages'))
  const [recipients] = useLoad(() => api('/messaging/recipients'))
  const [children] = useLoad(() => api('/children'))
  const [selected, setSelected] = useState(null)
  const [detail, setDetail] = useState(null)
  const [showCompose, setShowCompose] = useState(false)
  const [compose, setCompose] = useState({ recipientId: '', childId: '', subject: '', body: '' })
  const [reply, setReply] = useState('')
  const [run, err] = useAction()
  const current = conversations?.find((x) => x.id === selected)
  const childItems = children?.items ?? children ?? []
  const selectedRecipient = recipients?.find((x) => x.id === compose.recipientId)
  const childRequired = user.role === 'staff' && selectedRecipient?.role === 'parent' &&
    selectedRecipient.childCount <= 1

  useEffect(() => {
    const refresh = (event) => {
      if (event.detail?.type !== 'message') return
      reload()
      if (selected) open(selected)
    }
    window.addEventListener('app-notification', refresh)
    return () => window.removeEventListener('app-notification', refresh)
  }, [reload, selected])

  const open = async (id) => {
    setSelected(id)
    setDetail(await api(`/messages/${id}`))
    await markMessageNotificationsRead()
    reload()
  }
  const send = async (e) => {
    e.preventDefault()
    let created
    if (await run(async () => {
      created = await api('/messages', { method: 'POST', body: { ...compose, childId: compose.childId || null } })
    })) {
      setCompose({ recipientId: '', childId: '', subject: '', body: '' })
      setShowCompose(false)
      reload()
      setSelected(created.id)
      setDetail(await api(`/messages/${created.id}`))
    }
  }
  const sendReply = async (e) => {
    e.preventDefault()
    if (await run(() => api(`/messages/${selected}/reply`, { method: 'POST', body: { body: reply } }))) {
      setReply('')
      reload()
      open(selected)
    }
  }

  return (
    <>
      <div className="page-heading">
        <div><h1>Messages</h1><p className="muted">Private communication with families and daycare staff.</p></div>
        <button className="btn" onClick={() => setShowCompose(true)}>New message</button>
      </div>
      <Notice>{loadErr || err}</Notice>
      <section className="messages-layout panel">
        <aside className="conversation-list">
          <h2>Conversations</h2>
          {!conversations ? null : conversations.length === 0 ? <Empty>No messages yet.</Empty> : conversations.map((item) => (
            <button className={`conversation-item ${selected === item.id ? 'selected' : ''}`} key={item.id} onClick={() => open(item.id)}>
              <strong>{item.otherUser}</strong>
              <span>{item.subject}</span>
              <small>{item.child || 'General daycare message'} · {fmtDate(new Date(item.updatedAt).toLocaleDateString('en-CA'))}</small>
              {item.unread > 0 && <b className="unread">{item.unread}</b>}
            </button>
          ))}
        </aside>
        <div className="message-view">
          {!selected ? <Empty>Select a conversation or start a new message.</Empty> : !current || !detail ? null : (
            <>
              <div className="message-header"><div><h2>{detail.subject}</h2><p className="muted">With {detail.otherUser}{detail.child ? ` · ${detail.child}` : ''}</p></div></div>
              <div className="message-list">
                {detail.messages?.map((message) => (
                  <div className={`message-bubble ${message.senderId === user.id ? 'mine' : ''}`} key={message.id}>
                    <strong>{message.senderId === user.id ? 'You' : message.sender} <span className="message-role">{message.senderRole?.replace('_', ' ')}</span></strong>
                    <p>{message.body}</p>
                    <small>{fmtDate(new Date(message.sentAt).toLocaleDateString('en-CA'))} {fmtTime(message.sentAt)}</small>
                  </div>
                ))}
              </div>
              <form className="reply-form" onSubmit={sendReply}>
                <textarea required rows={3} value={reply} onChange={(e) => setReply(e.target.value)} placeholder="Write a reply…" />
                <button className="btn">Send reply</button>
              </form>
            </>
          )}
        </div>
      </section>
      {showCompose && <Modal title="New message" onClose={() => setShowCompose(false)}>
        <form className="stack" onSubmit={send}>
          <Field label="Send to">
            <select required value={compose.recipientId} onChange={(e) => setCompose({ ...compose, recipientId: e.target.value, childId: '' })}>
              <option value="">Choose a recipient…</option>
              {recipients?.map((recipient) => <option key={recipient.id} value={recipient.id}>{recipient.fullName} ({recipient.role})</option>)}
            </select>
          </Field>
          <Field label={`Related child${childRequired ? '' : ' (optional)'}`}>
            <select required={childRequired} value={compose.childId} onChange={(e) => setCompose({ ...compose, childId: e.target.value })}>
              <option value="">General message</option>
              {childItems.map((child) => <option key={child.id} value={child.id}>{child.firstName} {child.lastName}</option>)}
            </select>
          </Field>
          {childRequired && <p className="muted">Choose the child whose classroom you want to contact.</p>}
          {user.role === 'parent' && selectedRecipient?.role === 'staff' && <p className="muted">Choose a child to link this message to, or leave it as a general message.</p>}
          <Field label="Subject"><input required value={compose.subject} onChange={(e) => setCompose({ ...compose, subject: e.target.value })} /></Field>
          <Field label="Message"><textarea required rows={5} value={compose.body} onChange={(e) => setCompose({ ...compose, body: e.target.value })} /></Field>
          <div className="actions"><button className="btn">Send message</button><button type="button" className="btn ghost" onClick={() => setShowCompose(false)}>Cancel</button></div>
        </form>
      </Modal>}
    </>
  )
}
