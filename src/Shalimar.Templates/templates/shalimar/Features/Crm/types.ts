export type Id = string

export type TaskStatus = 'backlog' | 'todo' | 'in_progress' | 'blocked' | 'done'
export type TaskPriority = 'low' | 'medium' | 'high'
export type EpicStatus = 'planned' | 'active' | 'completed'

export interface User {
    id: Id
    name: string
    email: string
}

export interface Account {
    id: Id
    name: string
    industry: string
    tier: 'SMB' | 'Mid' | 'Enterprise'
    ownerId: Id
    updatedAt: string
}

export interface Contact {
    id: Id
    accountId: Id
    name: string
    title: string
    email: string
}

export interface Task {
    id: Id
    title: string
    status: TaskStatus
    priority: TaskPriority
    epicId?: Id
    accountId?: Id
    assigneeId?: Id
    collaboratorIds: Id[]
    dueAt?: string
    estimateMinutes?: number
    tags: string[]
    updatedAt: string
}

export type TaskActor = 'agent' | 'human'

export interface Epic {
    id: Id
    title: string
    description?: string
    status: EpicStatus
    updatedAt: string
}

export interface TaskMessage {
    id: Id
    taskId: Id
    ts: string
    actor: TaskActor
    author: string
    body: string
}

export type ArtifactKind = 'decision_log' | 'plan' | 'draft' | 'email' | 'call_summary' | 'notes' | 'other'

export interface TaskArtifact {
    id: Id
    taskId: Id
    ts: string
    kind: ArtifactKind
    title: string
    content: string
    createdBy: TaskActor
}

export interface TaskDecision {
    id: Id
    taskId: Id
    ts: string
    question: string
    options: string[]
    outcome: string
    rationale?: string
    madeBy: TaskActor
}

export interface ActivityItem {
    id: Id
    ts: string
    kind: 'task' | 'account' | 'system'
    summary: string
}

