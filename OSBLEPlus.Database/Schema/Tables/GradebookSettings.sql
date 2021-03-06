﻿CREATE TABLE [dbo].[GradebookSettings](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[CourseId] [int] NOT NULL,
	[SectionsEditable] [bit] NULL,
 CONSTRAINT [PK_GradebookSettings] PRIMARY KEY CLUSTERED 
(
	[Id] ASC,
	[CourseId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]