import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import ActivityTimeline from './ActivityTimeline'
import type { Activity } from '../types'

const buildActivity = (overrides: Partial<Activity> = {}): Activity => ({
  id: 'a1',
  timestamp: new Date('2026-05-06T10:00:00Z').toISOString(),
  type: 'entity',
  action: 'created',
  user: 'alice',
  description: 'Created a new entity',
  ...overrides,
})

describe('ActivityTimeline', () => {
  it('renders the description and user for a single activity', () => {
    render(<ActivityTimeline activities={[buildActivity()]} />)
    expect(screen.getByText('Created a new entity')).toBeInTheDocument()
    expect(screen.getByText('alice')).toBeInTheDocument()
  })

  it('renders an empty container when activities is empty', () => {
    const { container } = render(<ActivityTimeline activities={[]} />)
    expect(container.querySelectorAll('p').length).toBe(0)
  })

  it('groups multiple activities and shows their descriptions', () => {
    const activities = [
      buildActivity({ id: 'a1', description: 'first event' }),
      buildActivity({ id: 'a2', action: 'updated', description: 'second event' }),
      buildActivity({ id: 'a3', action: 'deleted', description: 'third event' }),
    ]
    render(<ActivityTimeline activities={activities} />)
    expect(screen.getByText('first event')).toBeInTheDocument()
    expect(screen.getByText('second event')).toBeInTheDocument()
    expect(screen.getByText('third event')).toBeInTheDocument()
  })

  it('renders environment badge when environment is present', () => {
    render(
      <ActivityTimeline
        activities={[buildActivity({ environment: 'qa2' })]}
      />,
    )
    expect(screen.getByText('qa2')).toBeInTheDocument()
  })

  it('truncates entityId to first 8 chars in display', () => {
    render(
      <ActivityTimeline
        activities={[buildActivity({ entityId: '0123456789abcdef' })]}
      />,
    )
    expect(screen.getByText(/01234567/)).toBeInTheDocument()
  })
})
