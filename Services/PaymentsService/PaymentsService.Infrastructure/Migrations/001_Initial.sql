-- Infrastructure/Migrations/001_CreateAllTables.sql

-- Таблица accounts
CREATE TABLE IF NOT EXISTS accounts (
    user_id UUID PRIMARY KEY,
    balance_amount DECIMAL(18, 2) NOT NULL,
    balance_currency VARCHAR(3) NOT NULL DEFAULT 'RUB',
    version INT NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    CONSTRAINT ck_accounts_balance_positive CHECK (balance_amount >= 0)
);

-- Таблица payments
CREATE TABLE IF NOT EXISTS payments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NOT NULL UNIQUE,
    user_id UUID NOT NULL,
    amount_amount DECIMAL(18, 2) NOT NULL,
    amount_currency VARCHAR(3) NOT NULL DEFAULT 'RUB',
    status VARCHAR(20) NOT NULL DEFAULT 'Pending',
    version INT NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMPTZ,
    
    CONSTRAINT ck_payments_status CHECK (status IN ('Pending', 'Success', 'Failed')),
    CONSTRAINT ck_payments_amount_positive CHECK (amount_amount > 0),
    CONSTRAINT fk_payments_accounts FOREIGN KEY (user_id) REFERENCES accounts(user_id) ON DELETE RESTRICT
);

-- Таблица withdrawals
CREATE TABLE IF NOT EXISTS withdrawals (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    payment_id UUID NOT NULL UNIQUE,
    amount_amount DECIMAL(18, 2) NOT NULL,
    amount_currency VARCHAR(3) NOT NULL DEFAULT 'RUB',
    success BOOLEAN NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    CONSTRAINT ck_withdrawals_amount_positive CHECK (amount_amount > 0),
    CONSTRAINT fk_withdrawals_payments FOREIGN KEY (payment_id) REFERENCES payments(id) ON DELETE CASCADE
);

-- Таблица inbox_messages
CREATE TABLE IF NOT EXISTS inbox_messages (
    id VARCHAR(255) PRIMARY KEY,
    order_id UUID NOT NULL,
    user_id UUID NOT NULL,
    body TEXT NOT NULL,
    message_type VARCHAR(50) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'Pending',
    retry_count INT NOT NULL DEFAULT 0,
    processor_id VARCHAR(255),
    locked_at TIMESTAMPTZ,
    received_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    processed_at TIMESTAMPTZ,
    error_message TEXT,
    version INT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    CONSTRAINT ck_inbox_messages_status CHECK (status IN ('Pending', 'Processing', 'Processed', 'Failed', 'DeadLetter')),
    CONSTRAINT ck_inbox_messages_max_retries CHECK (retry_count <= 10),
    CONSTRAINT fk_inbox_messages_accounts FOREIGN KEY (user_id) REFERENCES accounts(user_id) ON DELETE CASCADE
);
DROP TABLE IF EXISTS outbox_messages CASCADE;
-- Таблица outbox_messages
CREATE TABLE IF NOT EXISTS outbox_messages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    message_id UUID NOT NULL UNIQUE,
    correlation_id VARCHAR(255) NOT NULL,
    type VARCHAR(50) NOT NULL,
    body TEXT NOT NULL,
    topic VARCHAR(100) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'Pending',
    retry_count INT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    sent_at TIMESTAMPTZ,
    failed_at TIMESTAMPTZ,
    error_message TEXT,
    version INT NOT NULL DEFAULT 0,
    ALTER TABLE outbox_messages 
DROP CONSTRAINT ck_outbox_messages_status;


    CHECK (status IN ('Pending', 'Sending', 'Sent', 'Failed')),
    CONSTRAINT ck_outbox_messages_max_retries CHECK (retry_count <= 5)
);

-- Индексы для производительности
CREATE INDEX IF NOT EXISTS idx_payments_order_id ON payments(order_id);
CREATE INDEX IF NOT EXISTS idx_payments_user_id ON payments(user_id);
CREATE INDEX IF NOT EXISTS idx_payments_status ON payments(status);
CREATE INDEX IF NOT EXISTS idx_withdrawals_payment_id ON withdrawals(payment_id);
CREATE INDEX IF NOT EXISTS idx_inbox_messages_status ON inbox_messages(status, created_at) WHERE status IN ('Pending', 'Processing');
CREATE INDEX IF NOT EXISTS idx_inbox_messages_order_id ON inbox_messages(order_id);
CREATE INDEX IF NOT EXISTS idx_inbox_messages_user_id ON inbox_messages(user_id);
CREATE INDEX IF NOT EXISTS idx_outbox_messages_status ON outbox_messages(status, created_at);
CREATE INDEX IF NOT EXISTS idx_outbox_messages_correlation_id ON outbox_messages(correlation_id);
CREATE INDEX IF NOT EXISTS idx_outbox_messages_topic ON outbox_messages(topic);