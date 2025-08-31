using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using Discord.Rest;

namespace AribethBot.Helpers;

public static class ButtonPaginator
{
    public static async Task SendPaginatedEmbedsAsync(SocketInteractionContext ctx, List<Embed> pages, string title, bool isEphemeral = false)
    {
        if (pages == null || pages.Count == 0)
            throw new ArgumentException("No pages to display.");

        int currentPage = 0;
        MessageComponent components = BuildComponents(currentPage, pages.Count);

        // Send ephemeral first page
        RestFollowupMessage? message = await ctx.Interaction.FollowupAsync(
            embed: AddPageFooter(pages[currentPage], currentPage, pages.Count),
            components: components,
            ephemeral: isEphemeral // <-- make message ephemeral
        );

        async Task Handler(SocketMessageComponent component)
        {
            if (component.Message.Id != message.Id) return;

            if (component.User.Id != ctx.User.Id)
            {
                await component.RespondAsync("You cannot control this paginator.", ephemeral: true);
                return;
            }

            switch (component.Data.CustomId)
            {
                case "paginator_prev":
                    currentPage = Math.Max(currentPage - 1, 0); // don’t wrap around
                    break;
                case "paginator_next":
                    currentPage = Math.Min(currentPage + 1, pages.Count - 1); // don’t wrap around
                    break;
                
            }

            await component.UpdateAsync(msg =>
            {
                msg.Embed = AddPageFooter(pages[currentPage], currentPage, pages.Count);
                msg.Components = BuildComponents(currentPage, pages.Count);
            });
        }

        ctx.Client.ButtonExecuted += Handler;

        // Auto disable buttons after 5 minutes
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(5));
            ctx.Client.ButtonExecuted -= Handler;
            await message.ModifyAsync(m => m.Components = new ComponentBuilder().Build());
        });
    }

    private static MessageComponent BuildComponents(int currentPage, int totalPages)
    {
        return new ComponentBuilder()
            .WithButton("⏮️ Prev", "paginator_prev", disabled: currentPage == 0)
            .WithButton("⏭️ Next", "paginator_next", disabled: currentPage == totalPages - 1)
            .Build();
    }

    private static Embed AddPageFooter(Embed embed, int currentPage, int totalPages)
    {
        EmbedBuilder? builder = new EmbedBuilder()
            .WithTitle(embed.Title)
            .WithDescription(embed.Description)
            .WithColor(embed.Color ?? Color.Default)
            .WithFooter($"Page {currentPage + 1}/{totalPages}");

        if (embed.Timestamp.HasValue)
            builder.WithTimestamp(embed.Timestamp.Value);

        // Copy existing fields
        foreach (EmbedField field in embed.Fields)
            builder.AddField(field.Name, field.Value, field.Inline);

        // Copy author if exists
        if (embed.Author != null)
            builder.WithAuthor(embed.Author.Value.Name, embed.Author.Value.IconUrl, embed.Author.Value.Url);

        // Copy thumbnail, image, and other optional parts
        if (embed.Thumbnail != null)
            builder.WithThumbnailUrl(embed.Thumbnail.Value.Url);
        if (embed.Image != null)
            builder.WithImageUrl(embed.Image.Value.Url);
        if (embed.Url != null)
            builder.WithUrl(embed.Url);

        return builder.Build();
    }
}