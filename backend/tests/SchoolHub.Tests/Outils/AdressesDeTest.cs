using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;

namespace SchoolHub.Tests.Outils;

/// <summary>
/// Deux adresses qui n'existent que pour les tests, sous /api/test : l'application n'a pas encore
/// d'adresse réservée au propriétaire, ni de donnée d'un centre à lire.
/// Elles passent par la vraie session (<see cref="ConfigurationDesComptes.UtiliserLaSession"/>),
/// comme le reste de l'API.
/// </summary>
public sealed class AdressesDeTest : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> suite) =>
        app =>
        {
            // La branche est construite avant le pipeline de l'application : elle a son propre routage et sa propre session.
            app.MapWhen(
                contexte => contexte.Request.Path.StartsWithSegments("/api/test"),
                branche =>
                {
                    branche.UseRouting();
                    branche.UtiliserLaSession();
                    branche.UseEndpoints(adresses =>
                    {
                        adresses.MapGet("/api/test/proprietaire", () => "données du propriétaire")
                            .RequireAuthorization(Politiques.Proprietaire);

                        // Lue par identifiant, comme le ferait un module : seule la séparation des centres la protège.
                        adresses.MapGet("/api/test/notes/{id:guid}", async (Guid id, SchoolHubDbContext contexte) =>
                        {
                            var note = await contexte.Set<NoteDeTest>().FindAsync(id);
                            return note is null ? Results.NotFound() : Results.Text(note.Texte);
                        });
                    });
                });

            suite(app);
        };
}
