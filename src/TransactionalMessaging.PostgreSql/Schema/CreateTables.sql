-- TransactionalMessaging - PostgreSQL Schema
-- Creates both Outbox and Inbox tables with indexes

-- =============================================
-- Transactional Outbox Table
-- =============================================
CREATE TABLE transactional_outbox
(
    message_id VARCHAR(255) NOT NULL,
    message_type VARCHAR(500) NOT NULL,
    message_version VARCHAR(50) NOT NULL,

    payload BYTEA NOT NULL,
    content_type VARCHAR(100) NOT NULL,
    headers JSONB NULL,

    correlation_id VARCHAR(255) NULL,
    causation_id VARCHAR(255) NULL,

    ordering_key VARCHAR(255) NULL,
    ordering_sequence BIGINT NULL,

    state INTEGER NOT NULL, -- 0=Pending, 1=Claimed, 2=Published, 3=DeadLettered
    attempt_count INTEGER NOT NULL DEFAULT 0,
    next_attempt_at TIMESTAMP WITH TIME ZONE NULL,

    claim_token VARCHAR(50) NULL,
    claimed_by VARCHAR(100) NULL,
    claimed_until TIMESTAMP WITH TIME ZONE NULL,

    occurred_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
    published_at TIMESTAMP WITH TIME ZONE NULL,

    last_error_code VARCHAR(100) NULL,
    last_error_summary TEXT NULL,

    CONSTRAINT pk_transactional_outbox PRIMARY KEY (message_id)
);

-- Outbox Indexes
CREATE INDEX ix_transactional_outbox_state_next_attempt
    ON transactional_outbox (state, next_attempt_at);

CREATE INDEX ix_transactional_outbox_published_at
    ON transactional_outbox (published_at)
    WHERE state = 2;

CREATE INDEX ix_transactional_outbox_ordering
    ON transactional_outbox (ordering_key, ordering_sequence)
    WHERE ordering_key IS NOT NULL;

-- =============================================
-- Transactional Inbox Table
-- =============================================
CREATE TABLE transactional_inbox
(
    message_id VARCHAR(255) NOT NULL,
    consumer_scope VARCHAR(500) NOT NULL,
    payload_fingerprint VARCHAR(64) NULL,

    state INTEGER NOT NULL, -- 0=Reserved, 1=Completed, 2=Failed
    received_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
    completed_at TIMESTAMP WITH TIME ZONE NULL,
    attempt_count INTEGER NOT NULL DEFAULT 0,
    last_error TEXT NULL,

    CONSTRAINT pk_transactional_inbox PRIMARY KEY (message_id, consumer_scope)
);

-- Inbox Indexes
CREATE INDEX ix_transactional_inbox_state
    ON transactional_inbox (state);

CREATE INDEX ix_transactional_inbox_completed_at
    ON transactional_inbox (completed_at)
    WHERE state = 1;
