using Gaming.Game;

//#define USE_OPENAL
using System;
using System.Collections.Generic;
using System.IO;

#if USE_OPENAL
using OpenTK.Audio;
using OpenTK.Audio.OpenAL;
#endif

namespace Gaming.Audio
{
	public class Sound : GameObject
	{
		/*
		 * These are OpenAL "names" (or "objects"). They store and id of a buffer
		 * or a source object. Generally you would expect to see the implementation
		 * use values that scale up from '1', but don't count on it. The spec does
		 * not make this mandatory (as it is OpenGL). The id's can easily be memory
		 * pointers as well. It will depend on the implementation.
		 */
		public int m_BufferHandle;                                          // Buffers to hold sound data.
		public int m_Loop;

		#region GameObjectStuff

		public Sound()
		{
		}

		private static Sound LoadSerializationFileForFolder(String gameDataObjectXMLPath)
		{
			Sound soundLoaded;
			try
			{
				soundLoaded = (Sound)GameObject.Load(gameDataObjectXMLPath);
			}
			catch (FileNotFoundException)
			{
				soundLoaded = new Sound();
				soundLoaded.SaveXML(gameDataObjectXMLPath);
			}

			return soundLoaded;
		}

		public new static GameObject Load(String PathName)
		{
			// First we load up the Data In the Serialization file.
			String gameDataObjectXMLPath = Path.Combine(PathName, "Sound.xml");
			Sound soundLoaded = new Sound();// LoadSerializationFileForFolder(gameDataObjectXMLPath);

			String[] wavFilesArray = Directory.GetFiles(PathName, "*.wav");
			if (wavFilesArray.Length > 1 || wavFilesArray.Length < 1)
			{
				throw new System.Exception("You must have at leas and at most 1 loadable adio file in the dirrectory '" + PathName + "'.");
			}

			Sound loadingBuffer = new Sound();
#if USE_OPENAL
            // Variables to load into.
            int format;
            int size;
            byte[] data = null;
            int frequency;

            // Generate an OpenAL buffer.
            Al.alGenBuffers(1, out loadingBuffer.m_BufferHandle);
            if (Al.alGetError() != Al.AL_NO_ERROR)
            {
                return null;
            }

            AudioSystem.s_LoadedSounds.Add(loadingBuffer);

            // Attempt to locate the file.
            string fileName = wavFilesArray[0];
            //string fileName = "../../GameData/AsteroidExplosion.Sound/11_AsteroidExplosion.wav";

            if (!File.Exists(fileName))
            {
                return null;
            }

            // Load wav.
            Alut.alutLoadWAVFile(fileName, out format, out data, out size, out frequency, out loadingBuffer.m_Loop);
            if (data == null)
            {
                return null;
            }

            // Load wav data into the generated buffer.
            Al.alBufferData(loadingBuffer.m_BufferHandle, format, data, size, frequency);
            Alut.alutUnloadWAV(format, out data, size, frequency);

            // Do a final error check and then return.
            if (Al.alGetError() == Al.AL_NO_ERROR)
#endif
			{
				return loadingBuffer;
			}

			return null;
		}

		#endregion GameObjectStuff

		public SoundSource GetSoundSource()
		{
			SoundSource soundSource = new SoundSource();
			if (soundSource.BindToBuffer(this))
			{
				return soundSource;
			}

			return null;
		}

		private static List<SoundSource> s_AvailableSoundSources = new List<SoundSource>();
		private const int s_MaxSimultaneousSounds = 22;

		public void PlayAnAvailableCopy()
		{
			SoundSource soundToBindAndPlay = null;
			if (s_AvailableSoundSources.Count < s_MaxSimultaneousSounds)
			{
				soundToBindAndPlay = new SoundSource();
				s_AvailableSoundSources.Add(soundToBindAndPlay);
			}
			else
			{
				// Find the first sound not playing.
				foreach (SoundSource notPlayingSound in s_AvailableSoundSources)
				{
#if USE_OPENAL
                    int playingValue;
                    Al.alGetSourcei(notPlayingSound.m_SourceHandle, (int)Al.AL_SOURCE_STATE, out playingValue);
                    if (playingValue == (int)Al.AL_STOPPED)
                    {
                        soundToBindAndPlay = notPlayingSound;
                        break;
                    }
#endif
				}

				// TODO: No sound is available that is not playing.  Find the quietest sound and if it is quieter than
				// the sound we are about to play than play it.
			}

			if (soundToBindAndPlay != null)
			{
				soundToBindAndPlay.BindToBuffer(this);
				soundToBindAndPlay.Play();
			}
		}
	}
}