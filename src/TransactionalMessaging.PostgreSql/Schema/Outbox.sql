-- TransactionalMessaging Outbox Table for PostgreSQL
-- SPEC.md section 7 outbox record schema

CREATE TABLE transactional_outbox
(
    message_id VARCHAR(128) NOT NULL,
    message_type VARCHAR(256) NOT NULL,
    message_version VARCHAR(32) NOT NULL,

    payload BYTEA NOT NULL,
    content_type VARCHAR(128) NOT NULL,
    headers JSONB NULL,

    correlation_id VARCHAR(128) NULL,
    causation_id VARCHAR(128) NULL,

    ordering_key VARCHAR(256) NULL,
    ordering_sequence BIGINT NULL,

    state INTEGER NOT NULL, -- 0=Pending, 1=Claimed, 2=Published, 3=DeadLettered
    attempt_count INTEGER NOT NULL DEFAULT 0,
    next_attempt_at TIMESTAMP WITH TIME ZONE NULL,

    claim_token VARCHAR(128) NULL,
    claimed_by VARCHAR(256) NULL,
    claimed_until TIMESTAMP WITH TIME ZONE NULL,

    occurred_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
    published_at TIMESTAMP WITH TIME ZONE NULL,

    last_error_code VARCHAR(128) NULL,
    last_error_summary VARCHAR(2000) NULL,

    CONSTRAINT pk_transactional_outbox PRIMARY KEY (message_id)
);

-- Index for claiming pending messages using FOR UPDATE SKIP LOCKED
CREATE INDEX ix_transactional_outbox_claim
    ON transactional_outbox (state, next_attempt_at)
    WHERE state = 0; -- Pending only

-- Index for cleanup of published messages
CREATE INDEX ix_transactional_outbox_cleanup
    ON transactional_outbox (state, published_at)
    WHERE state = 2; -- Published only

-- Index for ordering key queries
CREATE INDEX ix_transactional_outbox_ordering_key
    ON transactional_outbox (ordering_key, ordering_sequence)
    WHERE ordering_key IS NOT NULL;
