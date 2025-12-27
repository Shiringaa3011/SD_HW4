CREATE TABLE IF NOT EXISTS orders (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL,
    amount DECIMAL(18, 2) NOT NULL,
    currency VARCHAR(3) DEFAULT 'RUB',
    description VARCHAR(500) NOT NULL,
    status VARCHAR(50) DEFAULT 'New',
    version INT DEFAULT 1,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    
    CONSTRAINT ck_orders_amount_positive CHECK (amount > 0)
);

CREATE TABLE IF NOT EXISTS outbox_messages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    type VARCHAR(100) NOT NULL,
    data TEXT NOT NULL,
    queue VARCHAR(100),
    order_id UUID,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    processed BOOLEAN DEFAULT FALSE,
    processed_at TIMESTAMPTZ,
    retry_count INT DEFAULT 0,
    error VARCHAR(1000)
);

CREATE TABLE IF NOT EXISTS processed_messages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    idempotency_key VARCHAR(255) UNIQUE NOT NULL,
    details TEXT NOT NULL,
    processed_at TIMESTAMPTZ DEFAULT NOW(),
    message_id VARCHAR(100),
    message_type VARCHAR(100),
    created_at TIMESTAMPTZ
);

CREATE INDEX idx_orders_user_id ON orders(user_id);
CREATE INDEX idx_orders_status ON orders(status);
CREATE INDEX idx_outbox_messages_processed ON outbox_messages(processed, created_at);
CREATE INDEX idx_processed_messages_key ON processed_messages(idempotency_key);