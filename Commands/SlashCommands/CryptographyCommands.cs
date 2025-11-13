using Discord.Interactions;

namespace AribethBot;

[Group("cryptography", "Commands related to cryptography")]
public class CryptographyCommands : InteractionModuleBase<SocketInteractionContext>
{
    // dependencies can be accessed through Property injection, public properties with public setters will be set by the service provider
    private Cryptography cryptography;
    
    // constructor injection is also a valid way to access the dependecies
    public CryptographyCommands()
    {
        cryptography = new Cryptography();
    }
    
    // Encrypt command
    [SlashCommand("encrypt", "Encrypt a message")]
    public async Task Encrypt([Summary("Cipher", "Cipher to use for the encryption")] Cryptography.Cipher cipher,
        [Summary("Shift", "Shift to use for the cipher (use number without decimals)")]
        int shift,
        [Summary("MessageToEncrypt", "Message to encrypt with the cipher")]
        string messageToEncrypt)
    {
        string[] parameters =
        [
            shift.ToString(),
            messageToEncrypt
        ];
        string result = cryptography.Encoder(cipher, parameters);
        // Return the encrypted text
        await RespondAsync($"Your text encrypted with the **{cipher}** cipher with a shift of **{shift}**\n```{result}```");
    }
    
    // Decrypt command
    [SlashCommand("decrypt", "Decrypt a message")]
    public async Task Decrypt([Summary("Cipher", "Cipher to use for the decryption")] Cryptography.Cipher cipher,
        [Summary("Shift", "Shift to use for the cipher (use number without decimals)")]
        int shift,
        [Summary("MessageToDecrypt", "Message to decrypt with the cipher")]
        string messageToDecrypt)
    {
        string[] parameters =
        [
            shift.ToString(),
            messageToDecrypt
        ];
        string result = cryptography.Decoder(cipher, parameters);
        // Return the decrypted text
        await RespondAsync($"Your text decrypted with the **{cipher}** cipher with a shift of **{shift}**\n```{result}```");
    }
}