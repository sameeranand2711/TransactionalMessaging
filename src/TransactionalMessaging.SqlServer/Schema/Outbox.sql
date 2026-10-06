-- TransactionalMessaging Outbox Table for SQL Server
-- SPEC.md section 7 outbox record schema

CREATE TABLE [dbo].[TransactionalOutbox]
(
    [MessageId] NVARCHAR(128) NOT NULL,
    [MessageType] NVARCHAR(256) NOT NULL,
    [MessageVersion] NVARCHAR(32) NOT NULL,

    [Payload] VARBINARY(MAX) NOT NULL,
    [ContentType] NVARCHAR(128) NOT NULL,
    [Headers] NVARCHAR(MAX) NULL, -- JSON key-value pairs

    [CorrelationId] NVARCHAR(128) NULL,
    [CausationId] NVARCHAR(128) NULL,

    [OrderingKey] NVARCHAR(256) NULL,
    [OrderingSequence] BIGINT NULL,

    [State] INT NOT NULL, -- 0=Pending, 1=Claimed, 2=Published, 3=DeadLettered
    [AttemptCount] INT NOT NULL DEFAULT 0,
    [NextAttemptAt] DATETIME2 NULL,

    [ClaimToken] NVARCHAR(128) NULL,
    [ClaimedBy] NVARCHAR(256) NULL,
    [ClaimedUntil] DATETIME2 NULL,

    [OccurredAt] DATETIME2 NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    [PublishedAt] DATETIME2 NULL,

    [LastErrorCode] NVARCHAR(128) NULL,
    [LastErrorSummary] NVARCHAR(2000) NULL,

    CONSTRAINT [PK_TransactionalOutbox] PRIMARY KEY CLUSTERED ([MessageId])
);

-- Index for claiming pending messages
CREATE NONCLUSTERED INDEX [IX_TransactionalOutbox_Claim]
    ON [dbo].[TransactionalOutbox] ([State], [NextAttemptAt])
    INCLUDE ([MessageId], [OrderingKey])
    WHERE [State] = 0; -- Pending only

-- Index for cleanup of published messages
CREATE NONCLUSTERED INDEX [IX_TransactionalOutbox_Cleanup]
    ON [dbo].[TransactionalOutbox] ([State], [PublishedAt])
    WHERE [State] = 2; -- Published only

-- Index for ordering key queries
CREATE NONCLUSTERED INDEX [IX_TransactionalOutbox_OrderingKey]
    ON [dbo].[TransactionalOutbox] ([OrderingKey], [OrderingSequence])
    WHERE [OrderingKey] IS NOT NULL;
