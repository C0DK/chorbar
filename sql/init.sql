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

CREATE TABLE user_event (
  email     TEXT                     NOT NULL,
  version   INTEGER                  NOT NULL CONSTRAINT positive_user_event_version CHECK (version > 0),
  timestamp TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
  payload   JSONB                    NOT NULL,

  PRIMARY KEY (email, version)
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

DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'chorbar-pod') THEN
    GRANT USAGE ON SCHEMA public TO "chorbar-pod";
    GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES    IN SCHEMA public TO "chorbar-pod";
    GRANT USAGE, SELECT, UPDATE          ON ALL SEQUENCES IN SCHEMA public TO "chorbar-pod";

    ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public
      GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES    TO "chorbar-pod";
    ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public
      GRANT USAGE, SELECT, UPDATE          ON SEQUENCES TO "chorbar-pod";
  END IF;
END
$$;
