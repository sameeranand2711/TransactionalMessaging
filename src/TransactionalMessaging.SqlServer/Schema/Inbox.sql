-- TransactionalMessaging Inbox Table for SQL Server
-- SPEC.md section 18 inbox/deduplication schema

CREATE TABLE [dbo].[TransactionalInbox]
(
    [ConsumerScope] NVARCHAR(256) NOT NULL,
    [MessageId] NVARCHAR(128) NOT NULL,
    [PayloadFingerprint] NVARCHAR(64) NULL,

    [State] INT NOT NULL, -- 0=Reserved, 1=Completed, 2=Failed
    [ReceivedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    [CompletedAt] DATETIME2 NULL,
    [AttemptCount] INT NOT NULL DEFAULT 0,
    [LastError] NVARCHAR(2000) NULL,

    CONSTRAINT [PK_TransactionalInbox] PRIMARY KEY CLUSTERED ([ConsumerScope], [MessageId])
);

-- Index for cleanup of completed messages
CREATE NONCLUSTERED INDEX [IX_TransactionalInbox_Cleanup]
    ON [dbo].[TransactionalInbox] ([State], [CompletedAt])
    WHERE [State] = 1; -- Completed only
