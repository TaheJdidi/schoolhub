using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolHub.Modules.Abonnements;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Abonnements;

/// <summary>EDI-01 à EDI-04 : créer un centre, saisir ses paiements, inviter et corriger son propriétaire.</summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class CentresEtPaiementsTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private ApplicationDeTest _application = null!;
    private BackOfficeDeTest _backOffice = null!;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, "abonnements_centres");
        await _application.InitialiserLaBaseAsync();
        _backOffice = new BackOfficeDeTest(_application);
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    private DateOnly Aujourdhui => DateDeTunis.Aujourdhui(_application.Horloge);

    [Fact]
    public async Task Le_centre_cree_est_en_attente_au_prix_de_l_offre_avec_ses_niveaux_et_sans_invitation()
    {
        var editeur = await _backOffice.EditeurAsync();
        var emailsAvant = _application.Emails.Envoyes.Count;

        var fiche = await BackOfficeDeTest.CreerAsync(editeur);

        Assert.Equal("EnAttente", fiche.Etat);
        Assert.Equal(90m, fiche.PrixMensuel);
        Assert.Null(fiche.PayeJusquAu);
        Assert.Equal(1, fiche.NombreDeComptes);
        Assert.Equal("Hedi", fiche.Proprietaire!.Prenom);
        Assert.Equal("98401220", fiche.Proprietaire.Telephone);
        Assert.Equal("Invite", fiche.Proprietaire.Etat);
        Assert.Equal(emailsAvant, _application.Emails.Envoyes.Count);

        var niveaux = await _application.DansUnScopeAsync(services =>
        {
            services.GetRequiredService<CentreCourant>().Definir(fiche.Id);
            return services.GetRequiredService<SchoolHubDbContext>().Niveaux.CountAsync();
        });
        Assert.True(niveaux > 0);

        var liste = await editeur.GetFromJsonAsync<List<LigneDeCentre>>("/api/editeur/centres");
        var ligne = Assert.Single(liste!, l => l.Id == fiche.Id);
        Assert.Equal("Hedi Ben Salah", ligne.Proprietaire);
        Assert.Equal("EnAttente", ligne.Etat);

        var journal = await editeur.GetFromJsonAsync<List<LigneDuJournalVue>>("/api/editeur/journal");
        Assert.Contains(journal!, l => l.Action == "CentreCree" && l.Centre == fiche.Nom && l.Auteur == "Nadia Mansour");
    }

    [Fact]
    public async Task Le_prix_se_donne_a_la_creation_et_se_change_ensuite()
    {
        var editeur = await _backOffice.EditeurAsync();
        var fiche = await BackOfficeDeTest.CreerAsync(editeur, BackOfficeDeTest.Jasmins(prix: 75m));
        Assert.Equal(75m, fiche.PrixMensuel);

        var reponse = await editeur.PutAsJsonAsync($"/api/editeur/centres/{fiche.Id}/prix", new PrixDuCentre(80.5m));
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal(80.5m, (await reponse.Content.ReadFromJsonAsync<FicheDuCentre>())!.PrixMensuel);

        var refus = await editeur.PutAsJsonAsync($"/api/editeur/centres/{fiche.Id}/prix", new PrixDuCentre(0m));
        Assert.Equal(HttpStatusCode.BadRequest, refus.StatusCode);
    }

    [Fact]
    public async Task Un_code_deja_pris_un_email_deja_utilise_ou_un_telephone_faux_sont_refuses()
    {
        var editeur = await _backOffice.EditeurAsync();
        var premier = BackOfficeDeTest.Jasmins();
        await BackOfficeDeTest.CreerAsync(editeur, premier);

        var memeCode = await editeur.PostAsJsonAsync("/api/editeur/centres", BackOfficeDeTest.Jasmins(code: premier.Code));
        Assert.Equal(HttpStatusCode.Conflict, memeCode.StatusCode);
        Assert.Contains("déjà pris", (await memeCode.Content.ReadFromJsonAsync<Avis>())!.Message);

        var memeEmail = await editeur.PostAsJsonAsync("/api/editeur/centres", BackOfficeDeTest.Jasmins(email: premier.EmailDuProprietaire));
        Assert.Equal(HttpStatusCode.Conflict, memeEmail.StatusCode);

        var telephone = await editeur.PostAsJsonAsync("/api/editeur/centres", BackOfficeDeTest.Jasmins() with { TelephoneDuProprietaire = "1234" });
        Assert.Equal(HttpStatusCode.BadRequest, telephone.StatusCode);
        Assert.Equal(BackOfficeDesCentres.TelephonePasValable, (await telephone.Content.ReadFromJsonAsync<Avis>())!.Message);

        var vide = await editeur.PostAsJsonAsync("/api/editeur/centres", BackOfficeDeTest.Jasmins() with { Code = " " });
        Assert.Equal(HttpStatusCode.BadRequest, vide.StatusCode);
    }

    [Fact]
    public async Task Un_paiement_rend_le_centre_actif_et_la_confirmation_part_au_proprietaire()
    {
        var editeur = await _backOffice.EditeurAsync();
        var fiche = await BackOfficeDeTest.CreerAsync(editeur);
        var du = Aujourdhui;
        var au = du.AddMonths(3).AddDays(-1);

        var paiement = await BackOfficeDeTest.PayerAsync(editeur, fiche.Id, BackOfficeDeTest.Paiement(du, au));

        Assert.True(paiement.ConfirmationEnvoyee);
        Assert.Equal("Actif", paiement.Fiche.Etat);
        Assert.Equal(au, paiement.Fiche.PayeJusquAu);
        var vu = Assert.Single(paiement.Fiche.Paiements);
        Assert.Equal(270m, vu.Montant);
        Assert.Equal("Nadia Mansour", vu.SaisiPar);
        Assert.Equal("Email", Assert.Single(vu.Confirmations).Canal);

        var email = _application.Emails.Envoyes.Last();
        Assert.Equal(fiche.Proprietaire!.Email, email.Destinataire);
        Assert.Contains(
            $"Paiement reçu : 270,000 TND, virement, du {MessagesAuProprietaire.Date(du)} au {MessagesAuProprietaire.Date(au)}, facture F-2027-003",
            email.Texte);
    }

    [Fact]
    public async Task Si_la_confirmation_ne_part_pas_le_paiement_reste_et_se_renvoie()
    {
        var editeur = await _backOffice.EditeurAsync();
        var fiche = await BackOfficeDeTest.CreerAsync(editeur);

        _application.Emails.Echouer = true;
        var paiement = await BackOfficeDeTest.PayerAsync(editeur, fiche.Id, BackOfficeDeTest.Paiement(Aujourdhui, Aujourdhui.AddMonths(1)));
        _application.Emails.Echouer = false;

        Assert.False(paiement.ConfirmationEnvoyee);
        Assert.Equal("Actif", paiement.Fiche.Etat);
        var id = Assert.Single(paiement.Fiche.Paiements).Id;

        var renvoi = await editeur.PostAsync($"/api/editeur/centres/{fiche.Id}/paiements/{id}/confirmation/email", null);
        Assert.Equal(HttpStatusCode.OK, renvoi.StatusCode);

        var whatsapp = await editeur.PostAsync($"/api/editeur/centres/{fiche.Id}/paiements/{id}/confirmation/whatsapp", null);
        var lien = await whatsapp.Content.ReadFromJsonAsync<LienWhatsApp>();
        Assert.StartsWith("https://wa.me/21698401220?text=Paiement%20re%C3%A7u", lien!.Lien);
        Assert.Equal(2, Assert.Single(lien.Fiche.Paiements).Confirmations.Count);
    }

    [Fact]
    public async Task Un_paiement_incomplet_ou_a_l_envers_est_refuse()
    {
        var editeur = await _backOffice.EditeurAsync();
        var fiche = await BackOfficeDeTest.CreerAsync(editeur);

        var sansFacture = await editeur.PostAsJsonAsync(
            $"/api/editeur/centres/{fiche.Id}/paiements", BackOfficeDeTest.Paiement(Aujourdhui, Aujourdhui.AddMonths(1), facture: " "));
        Assert.Equal(HttpStatusCode.BadRequest, sansFacture.StatusCode);

        var aLEnvers = await editeur.PostAsJsonAsync(
            $"/api/editeur/centres/{fiche.Id}/paiements", BackOfficeDeTest.Paiement(Aujourdhui, Aujourdhui.AddDays(-1)));
        Assert.Equal(BackOfficeDesCentres.PeriodePasValable, (await aLEnvers.Content.ReadFromJsonAsync<Avis>())!.Message);

        var modeInconnu = await editeur.PostAsJsonAsync(
            $"/api/editeur/centres/{fiche.Id}/paiements", BackOfficeDeTest.Paiement(Aujourdhui, Aujourdhui.AddMonths(1), mode: "Carte"));
        Assert.Equal(HttpStatusCode.BadRequest, modeInconnu.StatusCode);
    }

    [Fact]
    public async Task L_invitation_attend_le_premier_paiement_puis_part_par_email_ou_par_WhatsApp()
    {
        var editeur = await _backOffice.EditeurAsync();
        var fiche = await BackOfficeDeTest.CreerAsync(editeur);

        var tropTot = await editeur.PostAsync($"/api/editeur/centres/{fiche.Id}/invitation/email", null);
        Assert.Equal(HttpStatusCode.Conflict, tropTot.StatusCode);
        Assert.Equal(BackOfficeDesCentres.InvitationAvantLePaiement, (await tropTot.Content.ReadFromJsonAsync<Avis>())!.Message);

        await BackOfficeDeTest.PayerAsync(editeur, fiche.Id, BackOfficeDeTest.Paiement(Aujourdhui, Aujourdhui.AddMonths(1)));

        var whatsapp = await editeur.PostAsync($"/api/editeur/centres/{fiche.Id}/invitation/whatsapp", null);
        var lien = await whatsapp.Content.ReadFromJsonAsync<LienWhatsApp>();
        Assert.StartsWith("https://wa.me/21698401220?text=Bonjour%20Hedi", lien!.Lien);
        Assert.Contains("invitation%3Fcompte%3D", lien.Lien);
        Assert.Equal("WhatsApp", Assert.Single(lien.Fiche.Invitations).Canal);

        // Hedi choisit son mot de passe depuis l'email : il entre dans son centre.
        var hedi = await _backOffice.ProprietaireInscritAsync(editeur, fiche.Id);
        var moi = await hedi.GetFromJsonAsync<Moi>("/api/moi");
        Assert.Equal(fiche.Nom, moi!.Centre);
        Assert.Equal("Proprietaire", moi.Role);

        var apres = await BackOfficeDeTest.FicheAsync(editeur, fiche.Id);
        Assert.Equal(2, apres.Invitations.Count);
        Assert.Equal("Actif", apres.Proprietaire!.Etat);

        var plusDInvitation = await editeur.PostAsync($"/api/editeur/centres/{fiche.Id}/invitation/email", null);
        Assert.Equal(BackOfficeDesCentres.ProprietaireDejaInscrit, (await plusDInvitation.Content.ReadFromJsonAsync<Avis>())!.Message);
    }

    [Fact]
    public async Task Le_proprietaire_se_corrige_avant_l_invitation_et_son_email_se_fige_ensuite()
    {
        var editeur = await _backOffice.EditeurAsync();
        var fiche = await BackOfficeDeTest.CreerAsync(editeur);
        var nouvelEmail = $"hedi.bensalah.{BackOfficeDeTest.Unique()}@exemple.tn";

        var reponse = await editeur.PutAsJsonAsync(
            $"/api/editeur/centres/{fiche.Id}/proprietaire",
            new CorrectionDuProprietaire("Hédi", "Ben Salah", nouvelEmail, "+216 22 333 444"));
        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var corrige = (await reponse.Content.ReadFromJsonAsync<FicheDuCentre>())!.Proprietaire!;
        Assert.Equal("Hédi", corrige.Prenom);
        Assert.Equal(nouvelEmail, corrige.Email);
        Assert.Equal("22333444", corrige.Telephone);

        await BackOfficeDeTest.PayerAsync(editeur, fiche.Id, BackOfficeDeTest.Paiement(Aujourdhui, Aujourdhui.AddMonths(1)));
        await _backOffice.ProprietaireInscritAsync(editeur, fiche.Id);

        var fige = await editeur.PutAsJsonAsync(
            $"/api/editeur/centres/{fiche.Id}/proprietaire",
            new CorrectionDuProprietaire("Hédi", "Ben Salah", $"autre.{BackOfficeDeTest.Unique()}@exemple.tn", "22 333 444"));
        Assert.Equal(HttpStatusCode.Conflict, fige.StatusCode);
        Assert.Equal(BackOfficeDesCentres.EmailFigeApresLInscription, (await fige.Content.ReadFromJsonAsync<Avis>())!.Message);
    }

    [Fact]
    public async Task Un_centre_ouvert_par_la_commande_apparait_en_attente_au_prix_de_l_offre()
    {
        var editeur = await _backOffice.EditeurAsync();
        var centre = await _application.CreerCentreAsync("Centre Ancien");

        var fiche = await BackOfficeDeTest.FicheAsync(editeur, centre.Id);

        Assert.Equal("EnAttente", fiche.Etat);
        Assert.Equal(90m, fiche.PrixMensuel);
        Assert.Null(fiche.Proprietaire);
    }
}
