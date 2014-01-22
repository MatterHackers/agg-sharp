//#define USE_OPENAL
using System;
using System.Collections.Generic;
using System.Text;

#if USE_OPENAL
//using OpenTK.Audio;
using OpenTK.Audio.OpenAL;
#endif

namespace Gaming.Audio
{
    public static class AudioSystem
    {
        private static float[] listenerPosition = { 0, 0, 0 };                // Position of the Listener.
        private static float[] listenerVelocity = { 0, 0, 0 };                // Velocity of the Listener.

        // Orientation of the Listener. (first 3 elements are "at", second 3 are "up")
        // Also note that these should be units of '1'.
        private static float[] listenerOrientation = { 0, 0, -1, 0, 1, 0 };

        public static List<Sound> s_LoadedSounds = new List<Sound>();
        public static List<SoundSource> s_LoadedSoundSources = new List<SoundSource>();

        public static void Startup()
        {
            /*
            // Initialize OpenAL and clear the error bit.
            Alut.alutInit();
            Al.alGetError();

            Al.alListenerfv(Al.AL_POSITION, listenerPosition);
            Al.alListenerfv(Al.AL_VELOCITY, listenerVelocity);
            Al.alListenerfv(Al.AL_ORIENTATION, listenerOrientation);
             */
        }

        public static void Shutdown()
        {
#if USE_OPENAL
            foreach (Sound loadedSound in s_LoadedSounds)
            {
                AL.DeleteBuffers(1, ref loadedSound.m_BufferHandle);
            }
            s_LoadedSounds.Clear();
            foreach (SoundSource loadedSound in s_LoadedSoundSources)
            {
                AL.DeleteSources(1, ref loadedSound.m_SourceHandle);
            }
            s_LoadedSoundSources.Clear();
            //Alut.alutExit();
#endif
        }
    }
}
