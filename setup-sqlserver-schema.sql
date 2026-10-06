SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- Outbox Table
CREATE TABLE [dbo].[TransactionalOutbox]
(
    [MessageId] NVARCHAR(128) NOT NULL,
    [MessageType] NVARCHAR(256) NOT NULL,
    [MessageVersion] NVARCHAR(32) NOT NULL,
    [Payload] VARBINARY(MAX) NOT NULL,
    [ContentType] NVARCHAR(128) NOT NULL,
    [Headers] NVARCHAR(MAX) NULL,
    [CorrelationId] NVARCHAR(128) NULL,
    [CausationId] NVARCHAR(128) NULL,
    [OrderingKey] NVARCHAR(256) NULL,
    [OrderingSequence] BIGINT NULL,
    [State] INT NOT NULL,
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

CREATE NONCLUSTERED INDEX [IX_TransactionalOutbox_Claim]
    ON [dbo].[TransactionalOutbox] ([State], [NextAttemptAt])
    INCLUDE ([MessageId], [OrderingKey])
    WHERE [State] = 0;

CREATE NONCLUSTERED INDEX [IX_TransactionalOutbox_Cleanup]
    ON [dbo].[TransactionalOutbox] ([State], [PublishedAt])
    WHERE [State] = 2;

CREATE NONCLUSTERED INDEX [IX_TransactionalOutbox_OrderingKey]
    ON [dbo].[TransactionalOutbox] ([OrderingKey], [OrderingSequence])
    WHERE [OrderingKey] IS NOT NULL;

-- Inbox Table
CREATE TABLE [dbo].[TransactionalInbox]
(
    [ConsumerScope] NVARCHAR(256) NOT NULL,
    [MessageId] NVARCHAR(128) NOT NULL,
    [PayloadFingerprint] NVARCHAR(64) NULL,
    [State] INT NOT NULL,
    [ReceivedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    [CompletedAt] DATETIME2 NULL,
    [AttemptCount] INT NOT NULL DEFAULT 0,
    [LastError] NVARCHAR(2000) NULL,
    CONSTRAINT [PK_TransactionalInbox] PRIMARY KEY CLUSTERED ([ConsumerScope], [MessageId])
);

CREATE NONCLUSTERED INDEX [IX_TransactionalInbox_Cleanup]
    ON [dbo].[TransactionalInbox] ([State], [CompletedAt])
    WHERE [State] = 1;
