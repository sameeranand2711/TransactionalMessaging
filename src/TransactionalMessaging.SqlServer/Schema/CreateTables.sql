-- TransactionalMessaging - SQL Server Schema
-- Creates both Outbox and Inbox tables with indexes

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- =============================================
-- Transactional Outbox Table
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'transactional_outbox')
BEGIN
    CREATE TABLE transactional_outbox
    (
        message_id NVARCHAR(255) NOT NULL,
        message_type NVARCHAR(500) NOT NULL,
        message_version NVARCHAR(50) NOT NULL,

        payload VARBINARY(MAX) NOT NULL,
        content_type NVARCHAR(100) NOT NULL,
        headers NVARCHAR(MAX) NULL,

        correlation_id NVARCHAR(255) NULL,
        causation_id NVARCHAR(255) NULL,

        ordering_key NVARCHAR(255) NULL,
        ordering_sequence BIGINT NULL,

        state INT NOT NULL, -- 0=Pending, 1=Claimed, 2=Published, 3=DeadLettered
        attempt_count INT NOT NULL DEFAULT 0,
        next_attempt_at DATETIMEOFFSET NULL,

        claim_token NVARCHAR(50) NULL,
        claimed_by NVARCHAR(100) NULL,
        claimed_until DATETIMEOFFSET NULL,

        occurred_at DATETIMEOFFSET NOT NULL,
        created_at DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME(),
        published_at DATETIMEOFFSET NULL,

        last_error_code NVARCHAR(100) NULL,
        last_error_summary NVARCHAR(MAX) NULL,

        CONSTRAINT pk_transactional_outbox PRIMARY KEY CLUSTERED (message_id)
    );

    -- Outbox Indexes
    CREATE NONCLUSTERED INDEX ix_transactional_outbox_state_next_attempt
        ON transactional_outbox (state, next_attempt_at);

    CREATE NONCLUSTERED INDEX ix_transactional_outbox_published_at
        ON transactional_outbox (published_at)
        WHERE state = 2;

    CREATE NONCLUSTERED INDEX ix_transactional_outbox_ordering
        ON transactional_outbox (ordering_key, ordering_sequence)
        WHERE ordering_key IS NOT NULL;
END
GO

-- =============================================
-- Transactional Inbox Table
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'transactional_inbox')
BEGIN
    CREATE TABLE transactional_inbox
    (
        message_id NVARCHAR(128) NOT NULL,
        consumer_scope NVARCHAR(256) NOT NULL,
        payload_fingerprint NVARCHAR(64) NULL,

        state INT NOT NULL, -- 0=Reserved, 1=Completed, 2=Failed
        received_at DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME(),
        completed_at DATETIMEOFFSET NULL,
        attempt_count INT NOT NULL DEFAULT 0,
        last_error NVARCHAR(MAX) NULL,

        CONSTRAINT pk_transactional_inbox PRIMARY KEY CLUSTERED (message_id, consumer_scope)
    );

    -- Inbox Indexes
    CREATE NONCLUSTERED INDEX ix_transactional_inbox_state
        ON transactional_inbox (state);

    CREATE NONCLUSTERED INDEX ix_transactional_inbox_completed_at
        ON transactional_inbox (completed_at)
        WHERE state = 1;
END
GO
