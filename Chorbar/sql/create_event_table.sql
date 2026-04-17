CREATE TABLE user_event (
  identity     TEXT                     NOT NULL,
  version      INTEGER                  NOT NULL CONSTRAINT positive_version CHECK (version > 0),
  timestamp    TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
  payload      JSONB                    NOT NULL,

  PRIMARY KEY (identity, version)
);
