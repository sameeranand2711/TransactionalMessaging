-- TransactionalMessaging Inbox Table for PostgreSQL
-- SPEC.md section 18 inbox/deduplication schema

CREATE TABLE transactional_inbox
(
    consumer_scope VARCHAR(256) NOT NULL,
    message_id VARCHAR(128) NOT NULL,
    payload_fingerprint VARCHAR(64) NULL,

    state INTEGER NOT NULL, -- 0=Reserved, 1=Completed, 2=Failed
    received_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
    completed_at TIMESTAMP WITH TIME ZONE NULL,
    attempt_count INTEGER NOT NULL DEFAULT 0,
    last_error VARCHAR(2000) NULL,

    CONSTRAINT pk_transactional_inbox PRIMARY KEY (consumer_scope, message_id)
);

-- Index for cleanup of completed messages
CREATE INDEX ix_transactional_inbox_cleanup
    ON transactional_inbox (state, completed_at)
    WHERE state = 1; -- Completed only
