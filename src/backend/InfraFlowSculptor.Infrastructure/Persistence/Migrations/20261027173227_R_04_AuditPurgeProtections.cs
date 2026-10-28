using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraFlowSculptor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707", Justification = "The migration identifier is fixed by the R-04 plan.")]
    public partial class R_04_AuditPurgeProtections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.audit_events_reject_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $audit$
                BEGIN
                    IF TG_OP = 'DELETE'
                        AND pg_trigger_depth() > 1
                        AND current_setting('ifs.audit_purge', true) = 'active' THEN
                        RETURN OLD;
                    END IF;

                    RAISE EXCEPTION 'audit_events are append-only'
                        USING ERRCODE = '55000';
                END;
                $audit$;

                CREATE OR REPLACE FUNCTION public.perform_audit_purge_request()
                RETURNS trigger
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog, public
                AS $purge$
                DECLARE
                    effective_cutoff timestamptz;
                    previous_purge_context text;
                BEGIN
                    effective_cutoff := LEAST(
                        NEW.cutoff,
                        clock_timestamp() - INTERVAL '13 months');
                    previous_purge_context := current_setting('ifs.audit_purge', true);
                    PERFORM set_config('ifs.audit_purge', 'active', true);
                    DELETE FROM public.audit_events
                    WHERE occurred_at < effective_cutoff;
                    GET DIAGNOSTICS NEW.deleted_count = ROW_COUNT;
                    PERFORM set_config('ifs.audit_purge', COALESCE(previous_purge_context, ''), true);
                    RETURN NEW;
                END;
                $purge$;

                REVOKE ALL ON FUNCTION public.purge_audit_events(timestamptz) FROM PUBLIC;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.audit_events_reject_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $audit$
                BEGIN
                    IF pg_trigger_depth() > 1 THEN
                        IF TG_OP = 'DELETE' THEN
                            RETURN OLD;
                        END IF;

                        RETURN NEW;
                    END IF;

                    RAISE EXCEPTION 'audit_events are append-only'
                        USING ERRCODE = '55000';
                END;
                $audit$;

                CREATE OR REPLACE FUNCTION public.perform_audit_purge_request()
                RETURNS trigger
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog, public
                AS $purge$
                DECLARE
                    effective_cutoff timestamptz;
                BEGIN
                    effective_cutoff := LEAST(
                        NEW.cutoff,
                        clock_timestamp() - INTERVAL '13 months');
                    DELETE FROM public.audit_events
                    WHERE occurred_at < effective_cutoff;
                    GET DIAGNOSTICS NEW.deleted_count = ROW_COUNT;
                    RETURN NEW;
                END;
                $purge$;

                GRANT EXECUTE ON FUNCTION public.purge_audit_events(timestamptz) TO PUBLIC;
                """);
        }
    }
}
