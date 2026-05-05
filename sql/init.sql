CREATE SEQUENCE household_id_seq START 1;

CREATE TABLE household_event (
  household_id INTEGER                  NOT NULL CONSTRAINT positive_id CHECK(household_id > 0),
  version      INTEGER                  NOT NULL CONSTRAINT positive_version CHECK (version > 0),
  timestamp    TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
  created_by   TEXT                     NOT NULL,
  payload      JSONB                    NOT NULL,

  CONSTRAINT create_household_is_v1 CHECK (
    (version = 1) = (payload->>'Type' = 'create_household')
  ),

  PRIMARY KEY (household_id, version)
);

CREATE TABLE signin_otp (
  email TEXT NOT NULL,
  code TEXT NOT NULL,
  created TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now()
);

CREATE TABLE data_protection_key (
  friendly_name TEXT                     PRIMARY KEY,
  xml           TEXT                     NOT NULL,
  created       TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now()
);
