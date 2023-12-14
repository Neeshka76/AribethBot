using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AribethBot
{
    public class Cryptography
    {
        public enum Cipher
        {
            Caesar
        }

        public string Encoder(Cipher cipher, string[] parameters)
        {
            switch (cipher)
            {
                case Cipher.Caesar:
                    return CaesarCypher(true, parameters[0], parameters[1]);
            }
            return "Bad result, something is wrong Neeshka !";
        }

        public string Decoder(Cipher cipher, string[] parameters)
        {
            switch (cipher)
            {
                case Cipher.Caesar:
                    return CaesarCypher(false, parameters[0], parameters[1]);
            }
            return "Bad result, something is wrong Neeshka !";
        }

        private string CaesarCypher(bool encode, string shift, string message)
        {
            string output = "";
            if (encode)
            {
                foreach (char character in message)
                {
                    output += CipherChar(character, int.Parse(shift));
                }
            }
            else
            {
                foreach (char character in message)
                {
                    output += CipherChar(character, 26 - int.Parse(shift));
                }
            }
            return output;
        }

        private static char CipherChar(char ch, int key)
        {
            if (!char.IsLetter(ch))
            {
                return ch;
            }
            char d = char.IsUpper(ch) ? 'A' : 'a';
            return (char)((((ch + key) - d) % 26) + d);
        }

    }
}
