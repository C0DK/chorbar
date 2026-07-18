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
