CREATE TABLE watchlist_items (
    watchlist_id  NUMBER NOT NULL,
    company_id    NUMBER NOT NULL,
    target_price  NUMBER(14,2),
    added_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,

    CONSTRAINT pk_watchlist_items
        PRIMARY KEY (watchlist_id, company_id),

    CONSTRAINT ck_watchlist_items_target_price
        CHECK (target_price IS NULL OR target_price > 0),

    CONSTRAINT fk_watchlist_items_watchlist
        FOREIGN KEY (watchlist_id)
        REFERENCES watchlists (watchlist_id),

    CONSTRAINT fk_watchlist_items_company
        FOREIGN KEY (company_id)
        REFERENCES companies (company_id)
);